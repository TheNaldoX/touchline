using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Editor
{
    public static class KeeperHighClaimReview
    {
        static string activeOutput;static List<Frame> activeFrames;
        [Serializable] class Frame { public int scene,frame,side;public float height,alpha,contactFraction,ballHandGap,controlTime,leftWristSpeed,rightWristSpeed,readiness;public string action,file;public bool incomingContact,contactRecorded,earlyArrival;public Vector3 physicalBall,displayBall,hands,keeper,leftWrist,rightWrist; }
        [Serializable] class Report { public bool passed,synthetic=true,physicalAndroid=false,claimed,preContact;public int scenarios,contactFrames;public string failure;public Frame[] frames; }
        public static void Run()
        {
            try{
                string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineHighClaimOutput="))?.Substring("-touchlineHighClaimOutput=".Length)??"keeper-high-claim-review-v1";
                if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output name");
                var output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Preserve existing review evidence");Directory.CreateDirectory(output);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var frames=new List<Frame>();activeOutput=output;activeFrames=frames;int scene=0;
                foreach(bool early in new[]{true,false})foreach(int side in new[]{0,1})foreach(float height in new[]{1.1f,1.8f,2.2f}){
                    var db=new Database{clubs=new[]{new ClubData{id="claim-a",name="Synthetic blue",color="#247daa"},new ClubData{id="claim-b",name="Synthetic red",color="#bf432c"}},players=Enumerable.Range(0,40).Select(i=>new PlayerData{id="claim"+i,name="P"+i,team=i<20?"claim-a":"claim-b",position=i%20==0?"GB":i%20<5?"DEF":i%20<9?"MIL":"ATT",rating=80,heightCm=182}).ToArray()};
                    var career=new Career{club="claim-a"};career.lineup=Career.Select(db,career.club,career.tactic);var sim=MatchSimulation.Create(db,career,"claim-b",91,2700);var m=sim.State;
                    m.restart=0;m.phase="play";m.clock=100;m.decision=10;foreach(var actor in m.actors){actor.sentOff=true;actor.position=actor.previous=new Point(0,30);}
                    var keeper=m.actors[side*11];keeper.sentOff=false;int dir=-sim.Direction(side);keeper.position=keeper.previous=new Point(dir*49,0);keeper.angle=-dir*Mathf.PI*.5f;keeper.action="idle";keeper.actionTime=0;keeper.controlTime=0;keeper.velocity=new Point();
                    float initialElapsed=early?.1f:.4f,initialX=early?44:47;
                    m.ball=new BallState{kind="pass",side=1-side,lastTouch=1-side,from=m.actors[(1-side)*11+9].id,start=new Point(dir*43,.1f),end=new Point(dir*53,.1f),elapsed=initialElapsed,duration=1,position=new Point(dir*initialX,.1f),previous=new Point(dir*initialX,.1f),height=height,previousHeight=height,startHeight=height,endHeight=height};
                    var arena=new GameObject("High claim presentation").AddComponent<MatchArena>();arena.Initialize(db,sim);arena.enabled=false;arena.Speed=1;arena.Broadcast.Enabled=false;arena.Paused=false;
                    var target=new RenderTexture(960,640,24){antiAliasing=2};target.Create();arena.MatchCamera.targetTexture=target;arena.MatchCamera.aspect=1.5f;
                    bool claimed=false,preContact=false,preparedCapture=false;int contactFrames=0;float previousPhysical=height,maxFraction=0;string previousAction=keeper.action;
                    var visual=arena.PlayerVisual(side*11);var joints=visual.GetComponentsInChildren<Transform>();var left=joints.First(t=>t.name=="wrist.L");var right=joints.First(t=>t.name=="wrist.R");Vector3 previousLeft=Vector3.zero,previousRight=Vector3.zero;
                    for(int frame=1;frame<=110;frame++){
                        arena.RenderFrame(.01f);var contact=sim.KeeperContact;float alpha=Mathf.Clamp01((float)(m.remainder/.1));bool active=KeeperBallPresentation.Current(contact,m),incoming=active&&alpha<contact.fraction;
                        if(active){contactFrames++;maxFraction=Mathf.Max(maxFraction,contact.fraction);}
                        if(incoming){preContact=true;var a=new Vector3(contact.start.x,contact.startHeight,contact.start.z);var b=new Vector3(contact.impact.x,contact.height,contact.impact.z);if(Vector3.Distance(arena.BallDisplayPosition,Vector3.Lerp(a,b,alpha/contact.fraction))>.0001f)throw new Exception("Claim attached before physical impact");}
                        if(keeper.action=="claim"){
                            claimed=true;if(!MatchSimulation.HasRecordedKeeperClaim(keeper))throw new Exception("Missing recorded claim");
                            if(m.ball.height>previousPhysical+.0001f)throw new Exception("Gather rose above accepted catch height");previousPhysical=m.ball.height;
                        }
                        var hands=visual.HeldBallPosition;
                        var item=new Frame{scene=scene,frame=frame,side=side,height=height,alpha=alpha,contactFraction=contact.fraction,action=keeper.action,incomingContact=incoming,contactRecorded=active,controlTime=keeper.controlTime,keeper=new Vector3(keeper.position.x,0,keeper.position.z),physicalBall=new Vector3(m.ball.position.x,m.ball.height,m.ball.position.z),displayBall=arena.BallDisplayPosition,hands=hands,ballHandGap=Vector3.Distance(arena.BallDisplayPosition,hands),earlyArrival=early,leftWrist=left.position,rightWrist=right.position,leftWristSpeed=frame>1?Vector3.Distance(left.position,previousLeft)*100:0,rightWristSpeed=frame>1?Vector3.Distance(right.position,previousRight)*100:0,readiness=KeeperClaimAnticipation.Evaluate(m,keeper,alpha).weight};
                        previousLeft=left.position;previousRight=right.position;
                        bool preparationCapture=!preparedCapture&&item.readiness>.95f;
                        if(preparationCapture)preparedCapture=true;
                        if(frame==10||frame==15||frame==19||frame==20||frame==30||frame==50||frame==80||preparationCapture||keeper.action=="claim"&&previousAction!="claim"){
                            var center=new Vector3(dir*49,1,.1f);arena.MatchCamera.transform.position=center+new Vector3(-dir*4,2.4f,5);arena.MatchCamera.transform.LookAt(center);arena.MatchCamera.fieldOfView=38;
                            item.file=(early?"early-":"late-")+"claim-"+scene+"-"+frame.ToString("D3")+".png";
                            typeof(KeeperTransitionReview).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{arena.MatchCamera,target,Path.Combine(output,item.file)});
                        }
                        frames.Add(item);
                        previousAction=keeper.action;
                    }
                    // At fraction zero there is no pre-contact interval in
                    // this tick. Keep and report that legitimate case rather
                    // than requiring a frame that physically cannot exist.
                    if(!claimed||contactFrames==0||maxFraction>.101f&&!preContact){
                        string failure="scene="+scene+" claimed="+claimed+" preContact="+preContact+" contactFrames="+contactFrames+" maxFraction="+maxFraction+" actions="+string.Join(",",frames.Where(f=>f.scene==scene).Select(f=>f.action).Distinct());
                        File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=false,scenarios=scene+1,claimed=claimed,preContact=preContact,contactFrames=contactFrames,failure=failure,frames=frames.ToArray()},true));
                        throw new Exception(failure);
                    }
                    UnityEngine.Object.DestroyImmediate(arena.gameObject);target.Release();UnityEngine.Object.DestroyImmediate(target);scene++;
                }
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{passed=true,scenarios=scene,claimed=frames.Any(f=>f.action=="claim"),preContact=frames.Any(f=>f.incomingContact),contactFrames=frames.Count(f=>f.contactRecorded),frames=frames.ToArray()},true));Debug.Log("TOUCHLINE_KEEPER_HIGH_CLAIM_OK");EditorApplication.Exit(0);
            }catch(Exception e){
                if(activeFrames!=null&&!string.IsNullOrEmpty(activeOutput)&&!File.Exists(Path.Combine(activeOutput,"report.json")))
                    File.WriteAllText(Path.Combine(activeOutput,"report.json"),JsonUtility.ToJson(new Report{passed=false,failure=e.ToString(),claimed=activeFrames.Any(f=>f.action=="claim"),preContact=activeFrames.Any(f=>f.incomingContact),contactFrames=activeFrames.Count(f=>f.contactRecorded),frames=activeFrames.ToArray()},true));
                Debug.LogException(e);EditorApplication.Exit(1);
            }
        }
    }
}

