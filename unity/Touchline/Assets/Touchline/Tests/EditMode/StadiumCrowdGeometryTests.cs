using NUnit.Framework;
using UnityEngine;
namespace Touchline.Tests
{
 public class StadiumCrowdGeometryTests
 {
  [Test]
  public void CrowdConstructionCreatesNoSpectatorGameObjects()
  {
   int before=Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;var mesh=StadiumAtmosphere.Crowd("176","160",1);
   try{Assert.AreEqual(before,Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);Assert.Less(mesh.vertexCount,65535);Assert.AreEqual(7,mesh.subMeshCount);}finally{Object.DestroyImmediate(mesh);}
  }
  [Test]
  public void OpaqueSupporterGeometryContainsNoCollapsedTriangles()
  {
   var mesh=StadiumAtmosphere.Crowd("176","160",1);try{var v=mesh.vertices;var t=mesh.triangles;float minimum=float.MaxValue;for(int i=0;i<t.Length;i+=3){float area=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude;minimum=Mathf.Min(minimum,area);Assert.Greater(area,1e-10f,"A head/limb triangle must not collapse to a line.");}TestContext.WriteLine($"vertices={mesh.vertexCount} triangles={t.Length/3} minCrossSquared={minimum:E6}");}finally{Object.DestroyImmediate(mesh);}
  }
  [Test]
  public void NearestSupporterHasFlatCrownAndSeatedProjectingThighs()
  {
   var type=typeof(StadiumAtmosphere).GetNestedType("CrowdBuilder",System.Reflection.BindingFlags.NonPublic);var builder=System.Activator.CreateInstance(type);type.GetMethod("Person").Invoke(builder,new object[]{Vector3.zero,Vector3.forward,.5f,0,5,123456789u,true});var mesh=(Mesh)type.GetMethod("Build").Invoke(builder,null);
   try{var v=mesh.vertices;float top=mesh.bounds.max.y;int crown=0;float left=float.MaxValue,right=float.MinValue;bool lap=false;foreach(var point in v){if(Mathf.Abs(point.y-top)<.00001f){crown++;left=Mathf.Min(left,point.x);right=Mathf.Max(right,point.x);}if(point.y<0&&point.z>.25f)lap=true;}Assert.GreaterOrEqual(crown,4,"The head must end in a small crown, not a single cone point.");Assert.Greater(right-left,.08f);Assert.True(lap,"The first rows must show a seated thigh projection.");}finally{Object.DestroyImmediate(mesh);}
  }
 }
}
