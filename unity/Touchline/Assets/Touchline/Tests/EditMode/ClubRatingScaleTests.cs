using NUnit.Framework;
using Touchline.Core;

namespace Touchline.Tests
{
    public class ClubRatingScaleTests
    {
        [Test] public void SamePlayerIsEvaluatedRelativeToTheClub()
        {
            Assert.AreEqual("★★★☆☆",ClubRatingScale.Stars(75,75));
            Assert.AreEqual("★★★★★",ClubRatingScale.Stars(75,61));
            Assert.AreEqual("★☆☆☆☆",ClubRatingScale.Stars(75,89));
        }
        [Test] public void ObservationUncertaintyRemainsVisibleWithoutGlobalNumbers()
        {
            Assert.AreEqual("★★☆☆☆ à ★★★★☆",ClubRatingScale.Range(new ScoutRange{known=true,low=68,high=82},75));
            Assert.AreEqual("★★★☆☆",ClubRatingScale.Range(new ScoutRange{known=true,low=74,high=76},75));
            Assert.AreEqual("À observer",ClubRatingScale.Range(new ScoutRange{known=false,low=99,high=99},75));
        }
        [Test] public void RangeBarsUseTheSameReferenceAndStayWithinFiveStars()
        {
            Assert.AreEqual(3,ClubRatingScale.Relative(75,75));
            Assert.AreEqual(1,ClubRatingScale.Relative(1,90));
            Assert.AreEqual(5,ClubRatingScale.Relative(99,30));
        }
    }
}
