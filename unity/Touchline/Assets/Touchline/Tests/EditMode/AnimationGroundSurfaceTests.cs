using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class AnimationGroundSurfaceTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ThrowRecoveryKeepsBootsAbovePitch(int hz)
        {
            var go=new GameObject("Throw support regression");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="surface-throw",heightCm=180},0,9,Color.white);
                var actor=new Actor{slot=9,action="idle"};float dt=1f/hz;
                for(int i=0;i<hz;i++)view.Render(actor,1,dt);
                actor.actionSequence=1;actor.actionKind="throw";actor.actionContactTime=.5f;actor.actionHeight=1.8f;actor.actionTarget=new Point(.1f,.42f);
                var meshes=go.GetComponentsInChildren<MeshFilter>();float lowest=100;string part="";float when=0;
                for(int i=0;i<2*hz;i++){
                    float t=i*dt;actor.action=t<=1.1f?"throw":"idle";actor.actionTime=Mathf.Max(0,1.1f-t);view.Render(actor,1,dt,new Vector3(.1f,1.8f,.42f));
                    foreach(var mesh in meshes)Measure(mesh.sharedMesh.vertices,mesh.transform.localToWorldMatrix,mesh.name,t,ref lowest,ref part,ref when);
                }
                Assert.GreaterOrEqual(lowest,-.025f,"Boot below pitch at "+when+"s: "+part);
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase("kick",1,.11f)] [TestCase("header",1,2.05f)] [TestCase("throw",1,1.8f)]
        [TestCase("tackle",1,.11f)] [TestCase("miscontrol",1,.11f)] [TestCase("hurt",1,.11f)]
        [TestCase("dive",1,.22f)] [TestCase("dive",-1,.22f)]
        [TestCase("dive",1,1.1f)] [TestCase("dive",-1,1.1f)]
        [TestCase("dive",1,2.2f)] [TestCase("dive",-1,2.2f)]
        [TestCase("keeper-roll",1,.24f)] [TestCase("keeper-throw",1,1.78f)]
        [TestCase("control",1,.11f)] [TestCase("block",1,.4f)] [TestCase("block",1,1.1f)]
        [TestCase("claim",1,.7f)] [TestCase("keeper-hold",1,1.1f)]
        [TestCase("place-ball",1,.11f)] [TestCase("keeper-rise",1,1.1f)]
        [TestCase("idle",1,.11f)] [TestCase("run",1,.11f)] [TestCase("pivot",1,.11f)]
        [TestCase("dive",1,.22f,160)] [TestCase("dive",-1,.22f,205)]
        [TestCase("miscontrol",1,.11f,205)] [TestCase("keeper-roll",1,.24f,160)]
        [TestCase("dive",1,.22f,185,true)] [TestCase("dive",-1,.22f,185,true)]
        [TestCase("dive",1,1.1f,185,true)] [TestCase("dive",-1,1.1f,185,true)]
        [TestCase("dive",-1,.22f,160,true)] [TestCase("dive",1,2.2f,205,true)]
        [TestCase("tackle",1,.11f,160,false,true)] [TestCase("tackle",-1,.11f,205,false,true)]
        [TestCase("header",1,1.85f,160)] [TestCase("header",1,2.25f,205)]
        [TestCase("control-chest",1,1.25f,160)] [TestCase("control-chest",-1,1.60f,205)]
        [TestCase("control-thigh",1,.83f,160)] [TestCase("control-thigh",-1,1.07f,205)]
        [TestCase("fall",1,.11f,160)] [TestCase("fall",-1,.11f,205)]
        [TestCase("slide",1,.11f,160)] [TestCase("slide",-1,.11f,205)]
        [TestCase("aerial-contest",1,1.8f,160)] [TestCase("aerial-contest",-1,2.25f,205)]
        public void RenderedBodyAndBootSurfacesStayAboveThePitch(string action,int side,float height,int playerHeight=0,bool parry=false,bool withdrawn=false)
        {
            var go=new GameObject("Surface clearance "+action);var baked=new Mesh();
            try{
                bool keeper=action=="dive"||action=="claim"||action=="place-ball"||action.StartsWith("keeper-");var view=go.AddComponent<PlayerView>();
                view.Build(new PlayerData{id="surface-"+action,heightCm=playerHeight>0?playerHeight:keeper?185:180},0,keeper?0:9,Color.white);
                var actor=new Actor{slot=keeper?0:9,action="idle"};for(int i=0;i<20;i++)view.Render(actor,1,.02f);
                float duration=action=="dive"?1.2f:action=="throw"?1.1f:action=="hurt"?2:action=="tackle"?.75f:MatchSimulation.HandDistribution(action)?1.08f:action=="miscontrol"?.4f:action=="control"?.3f:action=="block"?.45f:action=="claim"?.8f:action=="place-ball"?MatchSimulation.KeeperPlaceDuration:action=="keeper-rise"?MatchSimulation.KeeperRiseDuration:action=="keeper-hold"||action=="idle"||action=="run"||action=="pivot"?3:.64f;
                actor.action=action;actor.actionKind=action=="dive"?(parry||height>2?"save-parry":"save-catch"):action=="tackle"?MatchSimulation.StandingDuel:action=="kick"?"pass":action;
                actor.actionSequence=1;actor.diveSide=side;actor.actionContactTime=action=="dive"?.18f:action=="tackle"?.2f:action=="header"?.12f:action=="throw"?.5f:.48f;
                actor.actionTarget=new Point(action=="dive"?side*1.1f:action=="tackle"?side*.85f:action.StartsWith("keeper-")?-.2f:.1f,.42f);actor.actionHeight=height;
                if(action==MatchSimulation.ChestControl||action==MatchSimulation.ThighControl){actor.action="control";duration=MatchSimulation.BodyControlDuration(action);actor.actionTarget=new Point(side*.12f,.3f);}
                if(action=="fall")duration=MatchSimulation.ContactFallDuration;
                if(action=="slide"){duration=MatchSimulation.SlidingDuelDuration;actor.actionTarget=new Point(side*.12f,.85f);}
                var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>();var boots=go.GetComponentsInChildren<MeshFilter>();float lowest=100;string lowestPart="";float lowestTime=0;
                for(int frame=0;frame<=130;frame++){
                    float time=frame*.02f;actor.actionTime=Mathf.Max(0,duration-time);if(time>duration)actor.action="idle";
                    if(withdrawn&&time>=.1f){actor.tackleWithdrawFrom=.65f;actor.actionTime=Mathf.Max(0,MatchSimulation.TackleRecovery*.35f-(time-.1f));if(actor.actionTime<=0)actor.action="idle";}
                    if(action=="run"){actor.velocity=new Point(0,3);actor.previous=actor.position;actor.position+=actor.velocity*.02f;actor.stride+=.06f;}
                    if(action=="pivot"){actor.action="idle";actor.angle=Mathf.Min(time,1)*Mathf.PI;}
                    var ball=new Vector3(actor.actionTarget.x,height,actor.actionTarget.z);
                    if(action==MatchSimulation.ChestControl||action==MatchSimulation.ThighControl){var controlled=new BallState{setupHeight=height,controlDuration=duration,controlElapsed=time};ball=Vector3.Lerp(ball,new Vector3(ball.x,.11f,.4f),MatchSimulation.BodyControlProgress(actor,controlled));}
                    if(MatchSimulation.HandDistribution(action)&&time<=.48f){var setup=new BallState{kind=action,setupStart=new Point(0,.36f),setupHeight=1.1f,start=actor.actionTarget,startHeight=height,end=new Point(0,18)};var p=MatchSimulation.KeeperDistributionPreparation(setup,time/.48f,out float y);ball=new Vector3(p.x,y,p.z);}
                    if(action=="place-ball")ball.y=Mathf.Lerp(1.1f,.11f,Mathf.SmoothStep(0,1,time/duration));
                    view.Render(actor,1,.02f,ball);
                    if(frame%5!=0)continue;
                    foreach(var skin in skins){skin.BakeMesh(baked);Measure(baked.vertices,skin.transform.localToWorldMatrix,skin.name,time,ref lowest,ref lowestPart,ref lowestTime);}
                    foreach(var boot in boots)Measure(boot.sharedMesh.vertices,boot.transform.localToWorldMatrix,boot.name,time,ref lowest,ref lowestPart,ref lowestTime);
                }
                TestContext.WriteLine(action+" minimum surface height "+lowest+" m at "+lowestTime+" s ("+lowestPart+")");
                Assert.GreaterOrEqual(lowest,parry?-.002f:-.025f,"Visible geometry below pitch: "+lowestPart+" at "+lowestTime+" seconds");
            }finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(go);}
        }
        static void Measure(Vector3[] vertices,Matrix4x4 matrix,string part,float time,ref float lowest,ref string lowestPart,ref float lowestTime)
        {
            foreach(var vertex in vertices){float y=matrix.MultiplyPoint3x4(vertex).y;if(y<lowest){lowest=y;lowestPart=part;lowestTime=time;}}
        }
    }
}
