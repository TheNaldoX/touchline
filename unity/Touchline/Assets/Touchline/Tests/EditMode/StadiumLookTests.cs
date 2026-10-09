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
        [Test] public void CachedMicrograinMatchesDirectTurfSampling()
        {
            var texture=PitchTurf.FieldTexture(true);try{
                foreach(int x in new[]{0,137,1000,2047})foreach(int y in new[]{0,333,1023}){
                    float px=((x+.5f)/PitchTurf.FieldWidth*2-1)*PitchTurf.HalfLength,pz=((y+.5f)/PitchTurf.FieldHeight*2-1)*PitchTurf.HalfWidth;
                    Color expected=(Color32)PitchTurf.FieldPixel(px,pz),actual=texture.GetPixel(x,y);
                    Assert.AreEqual(expected.r,actual.r,.004f);Assert.AreEqual(expected.g,actual.g,.004f);Assert.AreEqual(expected.b,actual.b,.004f);
                }
            }finally{Object.DestroyImmediate(texture);}
        }
        [Test] public void FullPitchDetailIsNonRepeatingAndAlignedAcrossShadeBoundary()
        {
            Assert.AreNotEqual(PitchTurf.FieldPixel(4,7),PitchTurf.FieldPixel(4+PitchTurf.GrainTile,7));
            foreach(bool shaded in new[]{false,true}){
                var mesh=PitchTurf.Surface(shaded,3,20);
                try{var vertices=mesh.vertices;var uv=mesh.uv;for(int i=0;i<vertices.Length;i++)Assert.AreEqual(PitchTurf.FieldUv(vertices[i]),uv[i]);}
                finally{Object.DestroyImmediate(mesh);}
            }
            var texture=PitchTurf.FieldTexture();try{
                Assert.AreEqual(2048,texture.width);Assert.AreEqual(1024,texture.height);
                Assert.Greater(texture.mipmapCount,1);Assert.AreEqual(TextureWrapMode.Clamp,texture.wrapMode);
                Assert.IsFalse(texture.isReadable,"CPU pixel buffer released after upload");
            }finally{Object.DestroyImmediate(texture);}
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
        static float Luma(Color c)=>c.r*.3f+c.g*.59f+c.b*.11f;
        [Test] public void SkyDarkensTowardsTheZenithAndCityLightsStayBelowBloom()
        {
            foreach(bool night in new[]{false,true}){
                if(!night)Assert.AreEqual(StadiumSky.HorizonColor(night),StadiumSky.SkyColor(night,0,0),"Horizon de l'après-midi sans nuage ni relief");
                Assert.Less(Luma(StadiumSky.Pixel(night,0,StadiumSky.Height-1)),Luma(StadiumSky.HorizonColor(night)),"Zénith plus sombre que l'horizon");
                Assert.Greater(StadiumSky.Pixel(night,7,StadiumSky.Height-1).b,StadiumSky.Pixel(night,7,StadiumSky.Height-1).r,"Zénith bleu");
            }
            float previous=float.MaxValue;
            for(float e=0;e<=StadiumSky.MaxElevation;e+=2.5f){float l=Luma(StadiumSky.SkyColor(true,e,0));Assert.LessOrEqual(l,previous+1e-5f,"Ciel du soir de plus en plus sombre vers le haut");previous=l;}
            int lit=0;
            for(int x=0;x<StadiumSky.Width;x++)for(int y=0;y<StadiumSky.Height;y++){
                var c=StadiumSky.Pixel(true,x,y);Assert.Less(Mathf.Max(c.r,Mathf.Max(c.g,c.b)),1f,"Rien d'assez clair pour le bloom (seuil 1,05)");
                if(c.r>.3f)lit++;
                if(StadiumSky.ElevationAt((y+.5f)/StadiumSky.Height)>6)Assert.Less(c.r,.3f,"Fenêtres seulement sur la silhouette de la ville");
            }
            Assert.Greater(lit,20,"Quelques fenêtres allumées le soir");
        }
        [Test] public void SkyDomeSurroundsTheCameraInsideTheFogFreeRadius()
        {
            var mesh=StadiumSky.Dome();
            try{
                Assert.Less(StadiumSky.Radius,110,"Sous le début du brouillard (MatchArena : 110 m au moins)");
                var v=mesh.vertices;var t=mesh.triangles;
                foreach(var p in v)Assert.AreEqual(StadiumSky.Radius,p.magnitude,1e-3f);
                for(int i=0;i<t.Length;i+=3){var a=v[t[i]];var n=Vector3.Cross(v[t[i+1]]-a,v[t[i+2]]-a);if(n.sqrMagnitude<1e-6f)continue;Assert.Less(Vector3.Dot(n,a+v[t[i+1]]+v[t[i+2]]),0,"Faces tournées vers la caméra au centre");}
                Assert.AreEqual(StadiumSky.MinElevation,StadiumSky.ElevationAt(0),1e-4f);Assert.AreEqual(90,StadiumSky.ElevationAt(1),1e-4f);
            }finally{Object.DestroyImmediate(mesh);}
            Assert.IsFalse(StadiumSky.Enabled(0),"Fond uni en qualité basse");Assert.IsTrue(StadiumSky.Enabled(1));
        }
        [Test] public void NightReplayKeepsTheFloodlightsBehindTheLowCamera()
        {
            // Plan bas du ralenti : du côté du but marqué, il regarde vers le centre (-side en x).
            for(int side=-1;side<=1;side+=2){
                var light=StadiumLighting.FloodRotation(StadiumLighting.FloodYawFacing(-side))*Vector3.forward;
                Assert.Greater(light.x*-side,0,"La lumière va dans le sens du regard : visages éclairés, pas de contre-jour");
                Assert.Less(light.y,0);Assert.Less(light.z,0,"Toujours depuis le côté de la caméra télé");
            }
            Assert.AreEqual(StadiumLighting.FloodYaw,StadiumLighting.FloodYawFacing(1),"Direct : orientation d'origine");
        }
        [Test] public void GoalFramesAreRoundTubesAroundTheGoalMouth()
        {
            var mesh=StadiumGeometry.GoalFrames();
            try{
                var v=mesh.vertices;var n=mesh.normals;
                for(int i=0;i<v.Length;i++){
                    Assert.AreEqual(52.5f,Mathf.Abs(v[i].x),StadiumGeometry.PostRadius+1e-4f);
                    // Pied des montants : normales vers l'extérieur du tube (lissées, aspect rond).
                    if(v[i].y<.01f){
                        var axis=new Vector3(Mathf.Sign(v[i].x)*52.5f,v[i].y,Mathf.Sign(v[i].z)*(StadiumGeometry.GoalHalfWidth+StadiumGeometry.PostRadius));
                        Assert.AreEqual(StadiumGeometry.PostRadius,(v[i]-axis).magnitude,1e-3f);Assert.Greater(Vector3.Dot(n[i],v[i]-axis),.9f*StadiumGeometry.PostRadius);
                    }
                    Assert.LessOrEqual(Mathf.Abs(v[i].z),StadiumGeometry.GoalHalfWidth+2*StadiumGeometry.PostRadius+1e-4f);
                }
            }finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void UpperTierStandsUpForHomeGoalsOnly()
        {
            var mesh=StadiumAtmosphere.UpperCrowd("176","160",1,out var rig);
            try{
                Assert.Greater(rig.People,0);var rest=mesh.vertices;var crowd=new CrowdReaction(mesh,rig);
                crowd.Trigger(1,CrowdReaction.Kind.Goal,new Vector3(52.5f,0,0));for(int i=0;i<30;i++)crowd.Advance(.05f);
                CollectionAssert.AreEqual(rest,mesh.vertices,"Second anneau : supporters du club recevant, assis sur un but visiteur");
                crowd.Trigger(0,CrowdReaction.Kind.Goal,new Vector3(-52.5f,0,0));for(int i=0;i<40;i++)crowd.Advance(.05f);
                var lifted=mesh.vertices;int raised=0;for(int i=0;i<rest.Length;i++)if(lifted[i].y-rest[i].y>.05f)raised++;
                Assert.Greater(raised,rest.Length/3,"Le second anneau se lève sur un but du club");
                for(float t=0;t<CrowdReaction.Duration(CrowdReaction.Kind.Goal)+1;t+=.1f)crowd.Advance(.1f);
                CollectionAssert.AreEqual(rest,mesh.vertices,"Tout le monde se rassoit exactement");
            }finally{Object.DestroyImmediate(mesh);}
        }
    }
}
