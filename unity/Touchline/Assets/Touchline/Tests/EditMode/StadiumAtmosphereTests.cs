using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Touchline.Tests
{
    public sealed class StadiumAtmosphereTests
    {
        [Test] public void FullCrowdFitsSixteenBitMeshAndSevenOpaqueDraws()
        {
            var mesh=StadiumAtmosphere.Crowd("176","160",1);
            try{
                Assert.Greater(mesh.vertexCount,0);Assert.Less(mesh.vertexCount,65535);Assert.AreEqual(StadiumAtmosphere.MaterialCount,mesh.subMeshCount);
                Assert.Less(mesh.triangles.Length/3,53000);Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt16,mesh.indexFormat);
                Assert.AreEqual(mesh.vertexCount,mesh.normals.Length);
                foreach(var normal in mesh.normals)Assert.That(normal.sqrMagnitude,Is.InRange(.98f,1.02f));
            }finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void SupportersRemainInStandsAndLeavePlayingAreaClear()
        {
            var mesh=StadiumAtmosphere.Crowd("176","160",1);
            // Standing supporters have hips 0.49 m above seated spectators.
            try{foreach(var v in mesh.vertices){Assert.IsTrue(Mathf.Abs(v.x)>59||Mathf.Abs(v.z)>38.5f);Assert.Greater(v.y,.3f);Assert.Less(v.y,8.5f);Assert.Less(Mathf.Abs(v.x),72);Assert.Less(Mathf.Abs(v.z),49);}}
            finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void CrowdIsDeterministicWithoutConsumingGlobalRandomState()
        {
            var previous=Random.state;Mesh first=null,second=null,different=null;
            try{
                Random.InitState(743);var saved=Random.state;first=StadiumAtmosphere.Crowd("176","160");
                float actual=Random.value;Random.state=saved;Assert.AreEqual(Random.value,actual);
                second=StadiumAtmosphere.Crowd("176","160");different=StadiumAtmosphere.Crowd("160","176");
                CollectionAssert.AreEqual(first.vertices,second.vertices);CollectionAssert.AreEqual(first.triangles,second.triangles);
                Assert.IsFalse(first.vertices.SequenceEqual(different.vertices),"Different home/away identities should vary the crowd layout");
            }finally{Random.state=previous;if(first!=null)Object.DestroyImmediate(first);if(second!=null)Object.DestroyImmediate(second);if(different!=null)Object.DestroyImmediate(different);}
        }
        [TestCase(0)][TestCase(-1)][TestCase(float.NaN)]
        public void EmptyOrInvalidOccupancyCreatesNoSpectators(float occupancy)
        {
            var mesh=StadiumAtmosphere.Crowd("176","160",occupancy);try{Assert.AreEqual(0,mesh.vertexCount);}finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void PaletteSeparatesClubsAndKeepsCrowdOpaque()
        {
            var colors=StadiumAtmosphere.Palette(Color.blue,Color.red);Assert.AreEqual(StadiumAtmosphere.MaterialCount,colors.Length);
            Assert.Greater(colors[0].b,colors[0].r);Assert.Greater(colors[4].r,colors[4].b);
            Assert.IsTrue(colors.All(c=>c.a==1));Assert.AreEqual(colors.Length,colors.Distinct().Count());
        }
        [Test] public void TechnicalEquipmentAndColourBoardsHaveBoundedGeometry()
        {
            var equipment=StadiumGeometry.TechnicalArea();
            try{
                Assert.Less(equipment.vertexCount,1500);
                foreach(var v in equipment.vertices)Assert.IsTrue(Mathf.Abs(v.z)>=33.97f,"Equipment must not intrude onto playable turf");
                Assert.Less(equipment.bounds.max.y,1.53f);
            }finally{Object.DestroyImmediate(equipment);}
        }
    }
}
