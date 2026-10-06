using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class DefensiveFacingTests
    {
        [TestCase("mark",0,-2)] [TestCase("cover",2,0)] [TestCase("shape",0,0)]
        public void CloseDefendersWatchBallWithoutChangingSimulation(string intent,float vx,float vz)
        {
            var go=new GameObject("Defensive orientation");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="defensive-watch",heightCm=182},0,2,Color.white);
                var actor=new Actor{slot=2,action=vx==0&&vz==0?"idle":"run",intent=intent,velocity=new Point(vx,vz),angle=Mathf.PI};
                var ball=new Vector3(0,.11f,9);
                for(int frame=0;frame<90;frame++){actor.previous=actor.position;actor.position+=actor.velocity/60;var before=JsonUtility.ToJson(actor);view.Render(actor,1,1f/60,ball,new PlayerMotionContext{defending=true});Assert.AreEqual(before,JsonUtility.ToJson(actor));Assert.Greater(view.FootPosition(true).y,-.025f);Assert.Greater(view.FootPosition(false).y,-.025f);}
                var direction=Vector3.ProjectOnPlane(ball-go.transform.position,Vector3.up).normalized;
                Assert.Greater(Vector3.Dot(view.transform.forward,direction),.98f);
                var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();
                foreach(var side in new[]{"L","R"}){
                    Transform wrist=null,elbow=null,middle=null,index=null,little=null;foreach(var bone in skin.bones){if(bone.name=="wrist."+side)wrist=bone;if(bone.name=="lowerarm01."+side)elbow=bone;if(bone.name=="finger3-1."+side)middle=bone;if(bone.name=="finger2-1."+side)index=bone;if(bone.name=="finger5-1."+side)little=bone;}
                    Assert.Greater(Vector3.Dot((wrist.position-elbow.position).normalized,(middle.position-wrist.position).normalized),.98f,"A relaxed defensive hand should continue the forearm rather than bend back");
                    var palm=Vector3.Cross(little.position-index.position,middle.position-wrist.position).normalized*(side=="L"?1:-1);
                    var expected=Vector3.ProjectOnPlane(go.transform.right*(side=="L"?-1:1),(wrist.position-elbow.position).normalized).normalized;
                    Assert.Greater(Vector3.Dot(palm,expected),.98f,"Relaxed hands face inwards rather than upwards");
                }
                if(vz<0)Assert.Less(view.transform.InverseTransformDirection(new Vector3(vx,0,vz)).z,-1.9f,"Retreat must use backward locomotion instead of turning away from play");
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(false)] [TestCase(true)] public void SprintingOrCarryingPreservesTravelFacing(bool carrying)
        {
            var go=new GameObject("Travel orientation");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="defensive-travel",heightCm=182},0,2,Color.white);
                var actor=new Actor{slot=2,action="run",intent="mark",velocity=new Point(0,carrying?-2:-7),angle=Mathf.PI};
                for(int frame=0;frame<90;frame++)view.Render(actor,1,1f/60,new Vector3(0,.11f,9),new PlayerMotionContext{carrying=carrying,defending=true});
                Assert.Greater(Vector3.Dot(view.transform.forward,Vector3.back),.99f);
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void PossessionAndRestartControlDefensiveContextAndPauseCache()
        {
            var actor=new Actor{id="defender",side=0,slot=2};var match=new MatchState{ball=new BallState{owner="opponent",side=1},phase="play",restart=0};
            var defending=PlayerMotionContext.From(match,actor,1);Assert.IsTrue(defending.defending);
            match.ball.side=0;var attacking=PlayerMotionContext.From(match,actor,1);Assert.IsFalse(attacking.defending);Assert.IsFalse(attacking.Same(defending));
            match.ball.side=1;match.restart=1;Assert.IsFalse(PlayerMotionContext.From(match,actor,1).defending);
        }
        [TestCase(0)] [TestCase(2)] public void KeeperAndAttackingSupportKeepSimulationFacing(int slot)
        {
            var go=new GameObject("Facing ownership");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="facing-owner"},0,slot,Color.white);
                var actor=new Actor{slot=slot,action="run",intent="shape",velocity=new Point(0,-2),angle=0};
                for(int i=0;i<90;i++)view.Render(actor,1,1f/60,new Vector3(10,.11f,0),new PlayerMotionContext{defending=slot==0});
                Assert.Less(Quaternion.Angle(go.transform.rotation,Quaternion.identity),.01f);
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(false)] [TestCase(true)] public void DefensiveRetreatIntoClearanceKeepsReleaseContact(bool left)
        {
            var go=new GameObject("Defensive clearance");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="retreat-clear",heightCm=182,preferredFoot=left?"Left":"Right"},0,2,Color.white);
                var actor=new Actor{slot=2,action="run",intent="mark",velocity=new Point(0,-2),angle=Mathf.PI};
                for(int i=0;i<60;i++){actor.previous=actor.position;actor.position+=actor.velocity/60;view.Render(actor,1,1f/60,new Vector3(0,.11f,9),new PlayerMotionContext{defending=true});}
                var previous=view.FootPosition(left);actor.previous=actor.position;actor.velocity=new Point();actor.action="kick";actor.actionKind="clearance";actor.actionSequence=1;actor.angle=0;
                var ball=new Vector3(actor.position.x,.11f,actor.position.z+.42f);actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=.11f;
                for(int frame=0;frame<=18;frame++){actor.actionTime=.64f-frame*.01f;view.Render(actor,1,.01f,ball);if(frame==0)Assert.Less(Vector3.Distance(previous,view.FootPosition(left)),.15f);}
                Assert.Less(Vector3.Distance(view.BootContactPosition(left),ball),.06f);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
