using NUnit.Framework;
using UnityEngine;

namespace Touchline.Tests
{
    public sealed class StadiumLookTests
    {
        [Test] public void TurfGrainTilesWithoutSeamAndStaysInRange()
        {
            for(int i=0;i<PitchTurf.Size;i+=7){
                Assert.AreEqual(PitchTurf.Brightness(i,0),PitchTurf.Brightness(i,PitchTurf.Size),1e-5f,"Couture horizontale du gazon");
                Assert.AreEqual(PitchTurf.Brightness(0,i),PitchTurf.Brightness(PitchTurf.Size,i),1e-5f,"Couture verticale du gazon");
                float v=PitchTurf.Brightness(i,(i*13)%PitchTurf.Size);Assert.That(v,Is.InRange(PitchTurf.GrainMin,PitchTurf.GrainMax));
            }
            var texture=PitchTurf.Grain();try{Assert.AreEqual(PitchTurf.Size,texture.width);Assert.AreEqual(TextureWrapMode.Repeat,texture.wrapMode);}finally{Object.DestroyImmediate(texture);}
        }
        [Test] public void UpperTierSupportersSitInTheFarStandUnderTheRoof()
        {
            var mesh=StadiumAtmosphere.UpperCrowd("176","160",1);
            try{
                Assert.Greater(mesh.vertexCount,0);Assert.Less(mesh.vertexCount,65535);Assert.AreEqual(StadiumAtmosphere.MaterialCount,mesh.subMeshCount);
                foreach(var v in mesh.vertices){Assert.Less(v.z,-(StadiumGeometry.UpperTierFront-1));Assert.Greater(v.y,StadiumGeometry.UpperTierBase-.5f);Assert.Less(v.y,StadiumGeometry.RoofHeight);Assert.Less(Mathf.Abs(v.x),60);}
            }finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void RoofAndUpperTierLeaveTheBroadcastSideOpen()
        {
            foreach(var mesh in new[]{StadiumGeometry.UpperStand(),StadiumGeometry.StadiumShell()})
                try{foreach(var v in mesh.vertices)Assert.IsTrue(v.z<-38||Mathf.Abs(v.x)>70,"Rien de haut côté caméra : "+mesh.name+" "+v);}
                finally{Object.DestroyImmediate(mesh);}
        }
    }
}
