using NUnit.Framework;
using UnityEngine;

namespace Touchline.Tests
{
    public class StadiumGeometryTests
    {
        [Test] public void GroundMarkingsFaceTheCameraAboveThePitchAndStayInBounds()
        {
            var mesh=StadiumGeometry.PitchMarkings();
            try{foreach(var normal in mesh.normals)Assert.Greater(normal.y,.99f,"Back-facing markings disappear from the broadcast camera");foreach(var v in mesh.vertices){Assert.That(v.y,Is.EqualTo(.025f).Within(.001f));Assert.LessOrEqual(Mathf.Abs(v.x),52.56f);Assert.LessOrEqual(Mathf.Abs(v.z),34.06f);}}
            finally{Object.DestroyImmediate(mesh);}
        }
    }
}
