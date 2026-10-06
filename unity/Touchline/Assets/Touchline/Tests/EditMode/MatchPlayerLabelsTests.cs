using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline.Tests
{
    public class MatchPlayerLabelsTests
    {
        [TestCase("Kylian Mbappé","Mbappé")][TestCase("Virgil van Dijk","van Dijk")][TestCase("Marc-André ter Stegen","ter Stegen")][TestCase("Alexis Mac Allister","Mac Allister")][TestCase("Kevin De Bruyne","De Bruyne")][TestCase("Neymar","Neymar")][TestCase("  José María Giménez  ","Giménez")]
        public void LabelsUseSurnamesAndPreserveFamilyParticles(string name,string expected)=>Assert.AreEqual(expected,MatchPlayerLabels.Surname(name));
        [TestCase("Cristian Cásseres Jr.","Venezuela","Cásseres")][TestCase("Kim Ji-Soo","South Korea","Kim")][TestCase("Son Heung-min","South Korea","Son")]
        public void SuffixesAndImportedFamilyFirstNamesAreHandled(string name,string nationality,string expected)=>Assert.AreEqual(expected,MatchPlayerLabels.Surname(name,nationality));
        [Test] public void DenseLabelsAreOmittedWhenNoShortLaneRemains()
        {
            var occupied=new Rect[3];var anchor=new Vector2(100,180);var size=new Vector2(400,250);
            for(int i=0;i<3;i++)Assert.IsTrue(MatchPlayerLabels.TryPlace(anchor,size,100,occupied,i,out occupied[i]));
            Assert.IsFalse(MatchPlayerLabels.TryPlace(anchor,size,100,occupied,3,out _),"A fourth overlapping label must not hide the first three");
        }
        [Test] public void ProjectionUsesViewportCoordinatesAndRejectsBehindCamera()
        {
            Assert.IsTrue(MatchPlayerLabels.Project(new Vector3(.25f,.75f,20),new Vector2(800,400),out var anchor));Assert.AreEqual(new Vector2(200,100),anchor);
            Assert.IsFalse(MatchPlayerLabels.Project(new Vector3(.5f,.5f,-1),new Vector2(800,400),out _));
            Assert.IsFalse(MatchPlayerLabels.Project(new Vector3(float.NaN,.5f,20),new Vector2(800,400),out _));
            Assert.IsFalse(MatchPlayerLabels.Project(new Vector3(1.01f,.5f,20),new Vector2(800,400),out _));
        }
        [Test] public void CongestedNamesUseShortSeparateLanesAndStayInsideViewport()
        {
            var occupied=new Rect[3];var anchor=new Vector2(100,180);var size=new Vector2(400,250);
            for(int i=0;i<3;i++)occupied[i]=MatchPlayerLabels.Place(anchor,size,100,occupied,i);
            Assert.IsFalse(occupied[0].Overlaps(occupied[1]));Assert.IsFalse(occupied[1].Overlaps(occupied[2]));Assert.LessOrEqual(occupied[0].y-occupied[2].y,40);
            var edge=MatchPlayerLabels.Place(new Vector2(399,4),size,100,occupied,0);Assert.GreaterOrEqual(edge.x,0);Assert.GreaterOrEqual(edge.y,0);Assert.LessOrEqual(edge.xMax,size.x);
        }
        [Test] public void RebindingReusesExactlyTwentyTwoLabelsAndHidingDisablesPicking()
        {
            var names=new MatchPlayerLabels();var original=names.LabelAt(0);var first=new Image();var second=new Image();
            names.Bind(null,first);Assert.AreEqual(22,names.Layer.childCount);names.Bind(null,second);
            Assert.AreSame(original,names.LabelAt(0));Assert.AreEqual(0,first.childCount);Assert.AreEqual(1,second.childCount);
            names.Hide();Assert.AreEqual(0,names.VisibleCount);Assert.IsNull(names.FindAt(Vector2.zero));names.Dispose();Assert.AreEqual(0,second.childCount);
        }
    }
}
