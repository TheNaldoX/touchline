using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static partial class MotionCapture
    {
        static int frame,lastGameFrame=-1;static Camera camera;static RenderTexture target;static PlayerView[] views;static Actor[] actors;static Transform[] footballs;static string output;
        static int detailFrame;static string pendingDetail;
        static MotionCapture(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("TouchlineMotionCapture",true);EditorApplication.isPlaying=true;}
        static void Tick()
        {
            if(!SessionState.GetBool("TouchlineMotionCapture",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null)return;
            if(Time.frameCount==lastGameFrame)return;lastGameFrame=Time.frameCount;
            try{
                if(camera==null){
                    output=Path.GetFullPath("../../artifacts/unity/motion");Directory.CreateDirectory(output);TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
                    var rig=new GameObject("Motion validation stage");var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.SetParent(rig.transform);ground.transform.localScale=new Vector3(3,1,3);ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.12f,.22f,.18f));
                    var light=new GameObject("Motion light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-35,0);RenderSettings.ambientLight=new Color(.65f,.7f,.75f);
                    camera=new GameObject("Motion camera").AddComponent<Camera>();camera.transform.position=new Vector3(8,3.8f,11);camera.transform.LookAt(new Vector3(0,.85f,1.5f));camera.fieldOfView=40;camera.backgroundColor=new Color(.09f,.14f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;target=new RenderTexture(1536,864,24);target.Create();camera.targetTexture=target;
                    views=new PlayerView[10];actors=new Actor[10];footballs=new Transform[10];string[] actions={"run","run","kick","header","dive","dive","claim","miscontrol","place-ball","keeper-rise"};
                    for(int i=0;i<10;i++){bool keeper=i>3&&i!=7;var go=new GameObject(actions[i]+i);views[i]=go.AddComponent<PlayerView>();views[i].Build(new PlayerData{id="pose"+i,preferredFoot=i==2?"Left":"Right"},0,keeper?0:9,i%2==0?Color.white:new Color(.2f,.45f,.7f));actors[i]=new Actor{id="pose"+i,slot=keeper?0:9,position=new Point(i<6?(i-2.5f)*1.8f:(i-7.5f)*2.2f,i<6?0:3.8f),action=actions[i],angle=0,diveSide=i==4?1:-1};if(i>=8){var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.transform.localScale=Vector3.one*.22f;footballs[i]=ball.transform;ball.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(Color.white);}}
                    return;
                }
                if(frame>=60){CaptureDetails();return;}
                if(frame>0&&frame%12==0)SaveCapture("pose-"+frame.ToString("D3"));
                frame++;float t=frame/60f;
                for(int i=0;i<10;i++){var actor=actors[i];actor.previous=actor.position;if(i<2){float speed=i==0?1.8f:5;actor.velocity=new Point(0,speed);actor.position.z=t*speed;actor.stride=t*speed;}else actor.actionTime=i<4?.64f-Mathf.Repeat(t,.64f):i<6?1.2f-t:i==6?.8f-Mathf.Repeat(t,.8f):i==8?MatchSimulation.KeeperPlaceDuration*(1-t):i==9?MatchSimulation.KeeperRiseDuration*(1-t):.4f-Mathf.Repeat(t,.4f);var point=new Vector3(actor.position.x,.3f,actor.position.z+.45f);if(i>=8){float phase=i==8?Mathf.SmoothStep(0,1,t):1;point=new Vector3(actor.position.x,Mathf.Lerp(1.1f,.11f,phase),actor.position.z+Mathf.Lerp(.36f,.42f,phase));footballs[i].position=point;}views[i].Render(actor,1,1f/60,point);}
            }catch(Exception error){SessionState.SetBool("TouchlineMotionCapture",false);Debug.LogException(error);EditorApplication.Exit(1);}
        }
        static void SaveCapture(string name){var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);RenderTexture.active=previous;}
        static void CaptureDetails()
        {
            if(pendingDetail!=null){SaveCapture(pendingDetail);pendingDetail=null;}
            if(detailFrame>=590){CaptureActionCoverage();return;}
            bool footwork=detailFrame>=270,backward=detailFrame>=430;
            int index=footwork?9:detailFrame<70?3:detailFrame<200?4:7;
            int local=footwork?detailFrame-(backward?430:270):detailFrame<70?detailFrame:detailFrame<200?detailFrame-70:detailFrame-200;
            float t=local*.01f;var actor=actors[index];
            if(local==0){
                for(int i=0;i<views.Length;i++)views[i].gameObject.SetActive(i==index);
                for(int i=0;i<footballs.Length;i++)if(footballs[i]!=null)footballs[i].gameObject.SetActive(false);
                camera.transform.position=new Vector3(3.1f,2.1f,4);camera.transform.LookAt(new Vector3(0,1,0));camera.fieldOfView=32;
                views[index].ChangeIdentity(new PlayerData{id="detail-"+index,heightCm=180,preferredFoot="Right"});
                actor.position=actor.previous=new Point();actor.angle=0;actor.velocity=new Point();actor.actionSequence=1;actor.actionTarget=new Point(.1f,.3f);actor.actionHeight=index==3?2.05f:.55f;actor.actionContactTime=.12f;
                views[index].transform.rotation=Quaternion.identity;
            }
            actor.action=footwork?"run":index==3?"header":index==4?"dive":t<.3f?"control":"idle";
            actor.actionTime=index==3?Mathf.Max(0,.64f-t):index==4?Mathf.Max(0,1.2f-t):Mathf.Max(0,.3f-t);
            var ball=index==3?new Vector3(.1f,2.05f,.3f+Mathf.Max(0,t-.12f)*5):index==4?new Vector3(.8f,.55f,.3f):new Vector3(.23f,.11f,.42f);
            if(footwork){actor.previous=actor.position;actor.velocity=backward?new Point(0,-2.5f):new Point(2.5f,0);actor.position+=actor.velocity*.01f;var center=new Vector3(actor.position.x,1,actor.position.z);camera.transform.position=center+new Vector3(3.1f,1.1f,4);camera.transform.LookAt(center);}
            views[index].Render(actor,1,.01f,ball);
            if(index!=4&&!footwork){footballs[8].gameObject.SetActive(true);footballs[8].position=ball;}
            if(index==3&&(local==6||local==12||local==18||local==32||local==60)||index==4&&(local==35||local==75||local==90||local==110||local==120)||index==7&&(local==12||local==29||local==60))pendingDetail=(index==3?"header":index==4?"dive-recovery":"control")+"-"+local.ToString("D3");
            if(footwork&&(local==40||local==60||local==80||local==120))pendingDetail=(backward?"backpedal":"sidestep")+"-"+local.ToString("D3");
            detailFrame++;
        }
        static readonly string[] coverage={"pass","cross","clearance","throw","tackle-left","tackle-right","block-low","block-high","miscontrol","hurt","dribble","keeper-ready","scorer","applause","disappointed","dive-low-left","dive-low-right","dive-mid-left","dive-mid-right","dive-high-left","dive-high-right","call-left","call-right","keeper-roll","keeper-throw","defend-backpedal","defend-shuffle","body-contact-left","body-contact-right","tackle-withdraw-left","tackle-withdraw-right","header-land-left","header-land-right","control-chest-left","control-chest-right","control-thigh-left","control-thigh-right","fall-left","fall-right","slide-left","slide-right","aerial-duel-left","aerial-duel-right"};
        static void CaptureActionCoverage()
        {
            int frame=detailFrame-590,index=frame/150,local=frame%150;
            if(index>=coverage.Length){if(CaptureAppearance())CaptureFilm();return;}
            var label=coverage[index];var actor=actors[7];bool aerialDuel=label.StartsWith("aerial-duel-");float t=local*(label.StartsWith("fall-")||label.StartsWith("slide-")?.02f:.01f);
            if(local==0){
                for(int i=0;i<views.Length;i++)views[i].gameObject.SetActive(i==7||aerialDuel&&i==3);
                if(aerialDuel){views[3].ChangeIdentity(new PlayerData{id="coverage-contestant-"+label,heightCm=180});views[3].transform.rotation=Quaternion.Euler(0,label.EndsWith("left")?-90:90,0);}
                views[7].ChangeIdentity(new PlayerData{id="coverage-"+label,heightCm=180,preferredFoot=label=="header-land-left"?"Left":"Right"});views[7].transform.rotation=Quaternion.identity;
                actor.position=actor.previous=new Point();actor.angle=0;actor.velocity=new Point();actor.stride=0;actor.actionSequence=index+1;actor.actionContactTime=label=="throw"?.5f:.18f;actor.actionKind=label;actor.slot=label=="keeper-ready"?0:9;
                actor.actionTarget=new Point(label=="tackle-left"?.85f:label=="tackle-right"?-.85f:0,.42f);actor.actionHeight=label=="throw"?1.8f:label=="block-high"?1.1f:.11f;
                actor.tackleWithdrawFrom=0;if(label.StartsWith("tackle-withdraw"))actor.actionTarget.x=label.EndsWith("left")?.85f:-.85f;
                camera.transform.position=new Vector3(3.1f,2.1f,4);camera.transform.LookAt(new Vector3(0,1,0));camera.fieldOfView=32;
            }
            float duration=label=="hurt"?2:label=="throw"?1.1f:label.StartsWith("tackle")?.75f:label.StartsWith("block")?.45f:label=="miscontrol"?.4f:.64f;
            actor.action=label.StartsWith("tackle")?"tackle":label.StartsWith("block")?"block":label=="pass"||label=="cross"||label=="clearance"?"kick":label=="throw"||label=="hurt"||label=="miscontrol"?label:"idle";
            if(t>duration)actor.action="idle";actor.actionTime=Mathf.Max(0,duration-t);
            var ball=new Vector3(actor.actionTarget.x,actor.actionHeight,.42f);
            if(label.StartsWith("tackle")){actor.actionKind=MatchSimulation.StandingDuel;actor.actionContactTime=.2f;ball+=new Vector3(Mathf.Sign(actor.actionTarget.x)*2,0,1)*Mathf.Max(0,t-.2f);}
            if(label.StartsWith("tackle-withdraw")){
                if(t>=.1f){actor.tackleWithdrawFrom=.65f;actor.actionTime=Mathf.Max(0,MatchSimulation.TackleRecovery*.35f-(t-.1f));if(actor.actionTime<=0)actor.action="idle";}
                ball=new Vector3(actor.actionTarget.x,.11f,.42f)+Vector3.forward*(6*Mathf.Max(0,t-.05f));
            }
            if(label.StartsWith("header-land")){actor.action=t<.64f?"header":"idle";actor.actionTime=Mathf.Max(0,.64f-t);actor.actionContactTime=.12f;actor.actionTarget=new Point(.1f,.3f);actor.actionHeight=2.05f;ball=new Vector3(.1f,2.05f,.3f)+Vector3.forward*(7*Mathf.Max(0,t-.12f));}
            if(label.StartsWith("control-")){
                actor.actionKind=label.Contains("chest")?MatchSimulation.ChestControl:MatchSimulation.ThighControl;
                float controlDuration=MatchSimulation.BodyControlDuration(actor.actionKind);actor.action=t<controlDuration?"control":"idle";actor.actionTime=Mathf.Max(0,controlDuration-t);
                actor.actionTarget=new Point(label.EndsWith("left")?.12f:-.12f,.30f);actor.actionHeight=label.Contains("chest")?1.40f:.95f;
                var control=new BallState{setupHeight=actor.actionHeight,controlElapsed=t,controlDuration=controlDuration};float progress=MatchSimulation.BodyControlProgress(actor,control);
                ball=Vector3.Lerp(new Vector3(actor.actionTarget.x,actor.actionHeight,.30f),new Vector3(actor.actionTarget.x,.11f,.40f),progress);
            }
            if(label.StartsWith("dive-")){
                int side=label.EndsWith("left")?1:-1;actor.slot=0;actor.diveSide=side;actor.actionContactTime=.18f;actor.actionKind=label.Contains("high")?"save-parry":"save-catch";
                actor.actionTarget=new Point(side*1.1f,.3f);actor.actionHeight=label.Contains("low")?.22f:label.Contains("high")?2.2f:1.1f;actor.action=t<1.2f?"dive":"idle";actor.actionTime=Mathf.Max(0,1.2f-t);
                ball=new Vector3(actor.actionTarget.x,actor.actionHeight,.3f)+Vector3.forward*Mathf.Max(0,.18f-t)*6;
                if(actor.actionKind=="save-parry"&&t>.18f)ball+=new Vector3(side*4,1,2)*(t-.18f);
            }
            if(label.StartsWith("fall-")){actor.action=t<MatchSimulation.ContactFallDuration?"fall":"idle";actor.actionTime=Mathf.Max(0,MatchSimulation.ContactFallDuration-t);actor.diveSide=label.EndsWith("left")?1:-1;}
            if(label.StartsWith("slide-")){actor.action=t<MatchSimulation.SlidingDuelDuration?"slide":"idle";actor.actionKind=MatchSimulation.SlidingDuel;actor.actionTime=Mathf.Max(0,MatchSimulation.SlidingDuelDuration-t);actor.diveSide=label.EndsWith("left")?1:-1;actor.actionTarget=new Point(actor.diveSide*.12f,.85f);actor.actionHeight=.11f;ball=new Vector3(actor.actionTarget.x,.11f,.85f)+Vector3.forward*(4*Mathf.Max(0,t-MatchSimulation.SlidingDuelContact));}
            var motion=new PlayerMotionContext();
            if(label.StartsWith("body-contact-")){motion.contactDirection=new Point(label.EndsWith("left")?1:-1,0);motion.contactWeight=Mathf.SmoothStep(0,1,t/.2f)*(1-Mathf.SmoothStep(0,1,(t-.7f)/.5f));}
            if(label.StartsWith("defend-")){
                motion.defending=true;actor.slot=2;actor.intent="mark";actor.action="run";actor.angle=label=="defend-backpedal"?Mathf.PI:Mathf.PI*.5f;
                actor.velocity=label=="defend-backpedal"?new Point(0,-2):new Point(2,0);actor.previous=actor.position;actor.position+=actor.velocity*.01f;
                ball=new Vector3(0,.11f,9);var center=new Vector3(actor.position.x,1,actor.position.z);camera.transform.position=center+new Vector3(3.1f,1.1f,4);camera.transform.LookAt(center);
            }
            if(MatchSimulation.HandDistribution(label)){
                actor.slot=0;actor.action=t<MatchSimulation.KeeperDistributionDuration?label:"idle";actor.actionTime=Mathf.Max(0,MatchSimulation.KeeperDistributionDuration-t);actor.actionContactTime=.48f;actor.actionTarget=new Point(-.20f,.52f);actor.actionHeight=label=="keeper-roll"?.24f:1.78f;
                var setup=new BallState{kind=label,setupStart=new Point(0,.36f),setupHeight=1.1f,start=actor.actionTarget,startHeight=actor.actionHeight,end=new Point(0,18)};
                ball=DistributionFilmBall(setup,t);
            }
            if(label.StartsWith("call-")){motion.requestSide=label.EndsWith("left")?1:-1;motion.requestWeight=Mathf.SmoothStep(0,1,t/.18f)*(1-Mathf.SmoothStep(0,1,(t-.65f)/.38f));}
            if(label=="throw"){if(t<=.5f){var setup=new BallState{setupStart=new Point(0,.3f),setupHeight=.11f,start=actor.actionTarget,startHeight=actor.actionHeight,end=new Point(0,8)};var point=MatchSimulation.ThrowPreparation(setup,t/.5f,out float height);ball=new Vector3(point.x,height,point.z);}else ball+=Vector3.forward*(t-.5f)*5;}
            if(label=="dribble"){
                actor.action="run";actor.velocity=new Point(0,2);actor.previous=actor.position;actor.position+=actor.velocity*.01f;actor.stride+=.02f;motion.carrying=true;
                ball=new Vector3(0,.11f,actor.position.z+.30f+.26f*(.5f+.5f*Mathf.Sin(actor.stride*2.4f)));
                var center=new Vector3(0,1,actor.position.z);camera.transform.position=center+new Vector3(3.1f,1.1f,4);camera.transform.LookAt(center);
            }
            if(label=="scorer"||label=="applause"||label=="disappointed"){motion.reaction=label=="scorer"?2:label=="applause"?1:-1;motion.reactionTime=t;}
            if(aerialDuel){
                float side=label.EndsWith("left")?1:-1;actor.angle=side*Mathf.PI*.5f;actor.action=t<.64f?"header":"idle";actor.actionTime=Mathf.Max(0,.64f-t);actor.actionContactTime=.12f;actor.actionTarget=new Point(side*.28f,0);actor.actionHeight=2;
                if(local==0)views[7].transform.rotation=Quaternion.Euler(0,side*90,0);
                var opponent=actors[3];opponent.position=opponent.previous=new Point(side*.56f,0);opponent.angle=-actor.angle;opponent.velocity=new Point();opponent.slot=2;opponent.action=t<.64f?MatchSimulation.AerialContest:"idle";opponent.actionKind=MatchSimulation.AerialContest;opponent.actionTime=actor.actionTime;opponent.actionSequence=index+1;opponent.actionContactTime=.12f;opponent.actionTarget=actor.actionTarget;opponent.actionHeight=2;
                ball=new Vector3(side*(.28f+7*Mathf.Max(0,t-.12f)),2,0);views[3].Render(opponent,1,.01f,ball);
            }
            views[7].Render(actor,1,.01f,ball,motion);footballs[8].gameObject.SetActive(aerialDuel||index<11&&label!="hurt"||label.StartsWith("tackle-withdraw")||label.StartsWith("header-land")||label.StartsWith("control-")||label.StartsWith("slide-")||label.StartsWith("dive-")||MatchSimulation.HandDistribution(label));
            if(label.StartsWith("dive-")&&actor.actionKind=="save-catch"&&t>=.18f)ball=views[7].HeldBallPosition;footballs[8].position=ball;
            if(local==0||local==5||local==10||local==18||local==20||local==35||local==48||local==50||local==60||local==68||local==80||local==88||local==90||local==102||local==140)pendingDetail="coverage-"+label+"-"+local.ToString("D3");
            detailFrame++;
        }
    }
}

