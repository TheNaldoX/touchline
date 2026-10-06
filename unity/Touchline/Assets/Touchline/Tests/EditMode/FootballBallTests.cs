using NUnit.Framework;
using UnityEngine;

namespace Touchline.Tests
{
    public class FootballBallTests
    {
        [Test] public void PanelMeshIsSharedRoundAndFacesOutward()
        {
            var mesh=FootballBallMesh.Shared;Assert.AreSame(mesh,FootballBallMesh.Shared);Assert.AreEqual(2,mesh.subMeshCount);
            Assert.AreEqual(20*6*4*3,mesh.GetIndexCount(0));Assert.AreEqual(12*5*4*3,mesh.GetIndexCount(1));
            var vertices=mesh.vertices;foreach(var v in vertices)Assert.That(v.magnitude,Is.EqualTo(.5f).Within(.0001f));
            var indices=mesh.triangles;for(int i=0;i<indices.Length;i+=3){var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];Assert.Greater(Vector3.Dot(Vector3.Cross(b-a,c-a),a+b+c),0);}
        }
        [Test] public void RollingAngleMatchesTravelAndFreezesWhenPausedOrHeld()
        {
            var spin=new BallVisualRotation();spin.Advance(Vector3.up*.11f,false,.1f);var point=new Vector3(0,.11f,.11f*Mathf.PI/2);spin.Advance(point,false,.1f);
            Assert.That(Quaternion.Angle(Quaternion.identity,spin.Rotation),Is.EqualTo(90).Within(.01f));var before=spin.Rotation;
            spin.Advance(point+Vector3.forward,false,0);Assert.AreEqual(before,spin.Rotation);
            spin.Advance(point+Vector3.up,true,.1f);Assert.AreEqual(before,spin.Rotation);
        }
        [Test] public void StraightRollingIsIndependentOfRenderingCadence()
        {
            var a=new BallVisualRotation();var b=new BallVisualRotation();a.Advance(Vector3.up*.11f,false,.1f);b.Advance(Vector3.up*.11f,false,.1f);
            for(int i=1;i<=30;i++)a.Advance(new Vector3(i/30f,.11f,i/60f),false,1f/30);
            for(int i=1;i<=60;i++)b.Advance(new Vector3(i/60f,.11f,i/120f),false,1f/60);
            Assert.Less(Quaternion.Angle(a.Rotation,b.Rotation),.05f);
        }
    }
}
