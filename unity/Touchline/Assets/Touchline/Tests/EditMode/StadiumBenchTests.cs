using NUnit.Framework;
using UnityEngine;

namespace Touchline.Tests
{
    public class StadiumBenchTests
    {
        [Test] public void BenchesSeatSubstitutesAndStaffWithAStandingCoachInTheSameCrowdMesh()
        {
            var mesh=StadiumAtmosphere.CrowdWithBenches("176","160",StadiumAtmosphere.DefaultOccupancy,out var rig);
            try{
                Assert.Less(mesh.vertexCount,65535);Assert.AreEqual(StadiumAtmosphere.MaterialCount,mesh.subMeshCount,"Aucune matière ni appel de rendu en plus");
                var v=mesh.vertices;
                for(int side=-1;side<=1;side+=2){
                    int seated=0,coaches=0;
                    for(int p=0;p<rig.People;p++){
                        var seat=rig.seat[p];if(Mathf.Abs(seat.x-side*StadiumGeometry.BenchX)>8||seat.z<36||seat.z>38.2f)continue;
                        Assert.AreEqual(side>0,rig.visiting[p],"Le banc visiteur réagit aux buts visiteurs");
                        for(int i=rig.personStart[p];i<rig.End(p);i++){Assert.Greater(v[i].z,36.6f,"Derrière les panneaux, hors de la pelouse");Assert.GreaterOrEqual(v[i].y,-.1f);Assert.Less(v[i].y,2f);}
                        if(Mathf.Abs(seat.y-StadiumGeometry.BenchSeatTop)<.01f)seated++;else coaches++;
                    }
                    Assert.AreEqual(StadiumGeometry.BenchSeats,seated,"Banc complet (remplaçants + staff)");Assert.AreEqual(1,coaches,"Un entraîneur debout");
                }
            }finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void ClosedStandsStillKeepTheBenchesWithoutExceedingTheSharedGeometryBudget()
        {
            var empty=StadiumAtmosphere.CrowdWithBenches("176","160",0,out var rig);
            var full=StadiumAtmosphere.CrowdWithBenches("176","160",1,out _);
            try{
                Assert.AreEqual(2*(StadiumGeometry.BenchSeats+1),rig.People);
                foreach(var p in empty.vertices){Assert.Greater(p.z,36.6f);Assert.Less(p.y,2f);}
                Assert.Less(full.vertexCount,65535);Assert.Less(full.triangles.Length/3,53000);
                Assert.AreEqual(StadiumAtmosphere.MaterialCount,full.subMeshCount);
            }finally{Object.DestroyImmediate(empty);Object.DestroyImmediate(full);}
        }
        [Test] public void SomeHomeEndSupportersStandWithLegsReachingTheirStep()
        {
            var mesh=StadiumAtmosphere.Crowd("176","160",1,out var rig);
            try{
                var v=mesh.vertices;int standing=0,homeEnd=0;
                for(int p=0;p<rig.People;p++){
                    var seat=rig.seat[p];if(seat.x*StadiumAtmosphere.HomeEndSide<59)continue;homeEnd++;
                    float low=float.MaxValue;for(int i=rig.personStart[p];i<rig.End(p);i++)low=Mathf.Min(low,v[i].y);
                    if(low<seat.y-.3f)standing++;
                }
                Assert.That(standing,Is.InRange(homeEnd/8,homeEnd/3),"Environ un supporter sur quatre debout hors des premiers rangs");
            }finally{Object.DestroyImmediate(mesh);}
        }
        [TestCase(-1)][TestCase(1)]
        public void SideTerracesHaveAisleStairsAndRailingsBehindTheBenches(int side)
        {
            var plain=StadiumGeometry.Stand(side,true);var terraces=StadiumGeometry.Stand(side,false);var end=StadiumGeometry.EndStand(side);
            try{
                foreach(var p in terraces.vertices)Assert.Greater(Mathf.Abs(p.z),38.3f,"Rien devant la tribune (bancs à 37,7 m)");
                Assert.Greater(terraces.bounds.max.y,5.2f+StadiumGeometry.RailHeight,"Rampe le long de l'escalier jusqu'au dernier rang");
                foreach(var p in end.vertices)Assert.Greater(Mathf.Abs(p.x),59,"Main courante du virage derrière les panneaux et le but");
                Assert.Less(terraces.vertexCount+end.vertexCount,6000,"Escaliers et rambardes restent légers");
            }finally{Object.DestroyImmediate(plain);Object.DestroyImmediate(terraces);Object.DestroyImmediate(end);}
        }
    }
}
