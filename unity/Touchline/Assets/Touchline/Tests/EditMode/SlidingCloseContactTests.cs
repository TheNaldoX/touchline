using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class SlidingCloseContactTests
    {
        [TestCase(145,1,30)] [TestCase(175,1,60)] [TestCase(215,1,120)]
        [TestCase(145,-1,30)] [TestCase(175,-1,60)] [TestCase(215,-1,120)]
        public void ACloseBallIsReachedByTuckingTheAnkleUnderThePelvis(int height,int side,int fps)
        {
            var go=new GameObject("Close sliding tackle");
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="close-slide",heightCm=height},0,2,Color.white);
                var root=new Vector3(31.013147f,0,16.831528f);float yaw=177.36676f;
                go.transform.position=root;go.transform.rotation=Quaternion.Euler(0,yaw,0);
                var actor=new Actor{slot=2,angle=yaw*Mathf.Deg2Rad,position=new Point(root.x,root.z),previous=new Point(root.x,root.z),action="idle"};
                for(int i=0;i<20;i++)view.Render(actor,1,1f/fps);
                var ball=root+go.transform.right*(side*.19747f)+go.transform.forward*.1400797f;ball.y=.11f;
                actor.action="slide";actor.actionKind=MatchSimulation.SlidingDuel;actor.actionSequence=1;actor.diveSide=side;
                actor.actionContactTime=MatchSimulation.SlidingDuelContact;actor.actionHeight=.11f;actor.actionTarget=new Point(ball.x,ball.z);
                var previous=view.BootContactPosition(side>0);float maxStep=0,contactDistance=100;
                int contactFrame=Mathf.RoundToInt(fps*MatchSimulation.SlidingDuelContact);
                for(int i=0;i<=contactFrame;i++){
                    actor.actionTime=MatchSimulation.SlidingDuelDuration-i/(float)fps;
                    view.Render(actor,1,1f/fps,ball);
                    var boot=view.BootContactPosition(side>0);maxStep=Mathf.Max(maxStep,Vector3.Distance(previous,boot));previous=boot;
                    if(i==contactFrame)contactDistance=Vector3.Distance(boot,ball);
                    Assert.AreEqual(root,go.transform.position,"Do not move the simulation root to create contact");
                    Assert.Greater(view.FootPosition(side>0).y,.025f,"The ankle remains above the pitch");
                }
                Assert.Less(contactDistance,.03f,"A near ball needs a flexed leg, not a forced forward ankle");
                Assert.Less(maxStep,.3f,"Preserve the prepared slide instead of snapping the boot at contact");
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
