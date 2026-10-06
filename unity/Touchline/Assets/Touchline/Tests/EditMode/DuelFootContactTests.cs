using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class DuelFootContactTests
    {
        [TestCase(.9f,.2f,180)] [TestCase(-.9f,.2f,180)]
        [TestCase(.2f,.95f,180)] [TestCase(-.2f,.95f,180)]
        [TestCase(.65f,-.65f,180)] [TestCase(-.65f,-.65f,180)]
        [TestCase(.9f,.2f,160)] [TestCase(-.9f,.2f,205)]
        [TestCase(.9f,.2f,180,90)] [TestCase(-.9f,.2f,180,270)]
        [TestCase(.4f,.4f,180,0,"block",.4f)] [TestCase(-.4f,.4f,180,90,"block",.6f)]
        public void DuelBootReachesItsRecordedContact(float x,float z,int height,float angle=0,string kind="tackle",float ballHeight=.11f)
        {
            var go=new GameObject("Tackle contact");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="duel",heightCm=height},0,9,Color.white);
                var rotation=Quaternion.Euler(0,angle,0);go.transform.rotation=rotation;
                var actor=new Actor{slot=9,action="run",velocity=new Point(0,2),angle=angle*Mathf.Deg2Rad};
                for(int i=0;i<30;i++)view.Render(actor,1,1f/60);
                float duration=kind=="tackle"?.55f:.45f;
                actor.action=kind;actor.actionSequence=1;actor.actionTime=duration;
                var ball=rotation*new Vector3(x,ballHeight,z);
                actor.actionTarget=new Point(ball.x,ball.z);actor.actionHeight=ballHeight;
                view.Render(actor,1,1f/60,ball);
                Assert.Less(Mathf.Min(Vector3.Distance(view.BootContactPosition(true),ball),Vector3.Distance(view.BootContactPosition(false),ball)),.13f,"Boot surface must reach the recorded impact");
                Assert.That(view.FootPosition(x<0).y,Is.InRange(.04f,.16f),"Support ankle remains grounded");
                Assert.AreEqual(Vector3.zero,go.transform.position,"Presentation must not move the simulation root");
                for(int i=1;i<=90;i++){
                    actor.actionTime=Mathf.Max(0,duration-i/60f);if(i/60f>duration){actor.action="idle";actor.velocity=new Point();}
                    view.Render(actor,1,1f/60,ball);
                    Assert.That(view.FootPosition(true).y,Is.GreaterThan(.025f));
                    Assert.That(view.FootPosition(false).y,Is.GreaterThan(.025f));
                }
                Assert.Less(go.transform.Find("Rig").localPosition.magnitude,.08f,"Recover the body after the lunge");
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
