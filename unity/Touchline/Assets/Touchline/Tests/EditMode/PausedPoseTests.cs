using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class PausedPoseTests
    {
        [Test] public void NewWallIntentRefreshesPausedPose(){var c=new PausedPoseCache();var a=new Actor{id="a"};c.NeedsUpdate(0,a,Vector3.zero);a.intent="wall";Assert.IsTrue(c.NeedsUpdate(0,a,Vector3.zero));}
        [Test] public void UnchangedPausedActorIsEvaluatedOnlyOnce(){var c=new PausedPoseCache();var a=new Actor{id="a"};Assert.IsTrue(c.NeedsUpdate(0,a,Vector3.zero));for(int i=0;i<120;i++)Assert.IsFalse(c.NeedsUpdate(0,a,Vector3.zero));}
        [Test] public void SubstituteRefreshesFrozenPose(){var c=new PausedPoseCache();var a=new Actor{id="a"};c.NeedsUpdate(0,a,Vector3.zero);a.id="b";Assert.IsTrue(c.NeedsUpdate(0,a,Vector3.zero));}
        [Test] public void NewBallPositionRefreshesHeadAndControlPose(){var c=new PausedPoseCache();var a=new Actor{id="a"};c.NeedsUpdate(0,a,Vector3.zero);Assert.IsTrue(c.NeedsUpdate(0,a,new Vector3(0,0,1)));}
        [Test] public void ActionAndContactChangesRefreshPose(){var c=new PausedPoseCache();var a=new Actor{id="a"};c.NeedsUpdate(0,a,Vector3.zero);a.action="kick";Assert.IsTrue(c.NeedsUpdate(0,a,Vector3.zero));a.actionTime=.5f;Assert.IsTrue(c.NeedsUpdate(0,a,Vector3.zero));a.actionTarget=new Point(1,1);Assert.IsTrue(c.NeedsUpdate(0,a,Vector3.zero));}
        [Test] public void ResetAfterResumeAllowsFreshPausedFrame(){var c=new PausedPoseCache();var a=new Actor{id="a"};c.NeedsUpdate(0,a,Vector3.zero);c.Reset();Assert.IsTrue(c.NeedsUpdate(0,a,Vector3.zero));}
    }
}
