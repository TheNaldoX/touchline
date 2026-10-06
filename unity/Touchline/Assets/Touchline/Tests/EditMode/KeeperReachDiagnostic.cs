// Diagnostic only: parent may copy into EditMode tests when Unity is stopped.
// Does not assert that an arbitrary distant target is physically reachable.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class KeeperReachDiagnostic
    {
        [Serializable] public class Row { public float targetLateral,height,rootTravel,nearestGloveSurface,catchCentreGap,maxHandStep,armLengthChange; public string kind; }
        [Serializable] public class Report { public string note;public List<Row> rows=new List<Row>(); }
        [Test] public void MeasureKeeperReachWithoutInflatingCollisionRadiusOrStretchingLimbs()
        {
            var report=new Report{note="180cm keeper. .35s contact. Root travel is a diagnostic controlled input, not proof that current Core supplies it. Glove distance is distance to ball centre; <=.11m means surface reaches ball."};
            foreach(float distance in new[]{1.1f,1.6f,2f,2.3f,2.6f})foreach(float height in new[]{.22f,1.1f,2.2f})foreach(float travel in new[]{0f,.6f,1f})foreach(string kind in new[]{"save-catch","save-parry"}){
                var go=new GameObject("Keeper reach envelope");var mesh=new Mesh();try{
                    var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="keeper-envelope",heightCm=180},0,0,Color.yellow);
                    Transform shoulder=null,elbow=null,wrist=null;
                    foreach(var bone in go.GetComponentsInChildren<Transform>()){if(bone.name=="upperarm01.L")shoulder=bone;if(bone.name=="lowerarm01.L")elbow=bone;if(bone.name=="wrist.L")wrist=bone;}
                    float armLength=Vector3.Distance(shoulder.position,elbow.position)+Vector3.Distance(elbow.position,wrist.position);
                    var actor=new Actor{slot=0,action="dive",actionKind=kind,actionSequence=1,actionContactTime=.35f,actionTarget=new Point(distance,.3f),actionHeight=height,diveSide=1};
                    var ball=new Vector3(distance,height,.3f);Vector3 last=default;float maxStep=0;
                    for(int frame=0;frame<=35;frame++){
                        float t=frame*.01f;actor.actionTime=1.2f-t;actor.previous=actor.position;actor.position=new Point(travel*Mathf.SmoothStep(0,1,t/.35f),0);
                        view.Render(actor,1,.01f,ball);if(frame>0)maxStep=Mathf.Max(maxStep,Vector3.Distance(last,wrist.position));last=wrist.position;
                    }
                    float closest=100;
                    foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.name.Contains("glove")||skin.name.Contains("Glove")){
                        skin.BakeMesh(mesh);foreach(var v in mesh.vertices)closest=Mathf.Min(closest,Vector3.Distance(skin.transform.TransformPoint(v),ball));
                    }
                    Assert.Less(closest,100,"Find actual glove geometry for diagnostic");
                    float armAfter=Vector3.Distance(shoulder.position,elbow.position)+Vector3.Distance(elbow.position,wrist.position);
                    report.rows.Add(new Row{targetLateral=distance,height=height,rootTravel=travel,kind=kind,nearestGloveSurface=closest,catchCentreGap=Vector3.Distance(view.HeldBallPosition,ball),maxHandStep=maxStep,armLengthChange=Mathf.Abs(armAfter-armLength)});
                    Assert.Less(Mathf.Abs(armAfter-armLength),.001f,"IK may rotate limbs, never stretch them");
                }finally{UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(go);}
            }
            File.WriteAllText(Path.GetFullPath("../../artifacts/unity/keeper-reach-diagnostic.json"),JsonUtility.ToJson(report,true));
        }
    }
}
