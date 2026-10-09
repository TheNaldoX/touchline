using System;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Touchline.Tests
{
    public sealed class StadiumLightingTests
    {
        [Test] public void NightFillKeepsVerticalFacesReadableWithoutAnExtraLight()
        {
            var root=new GameObject("Night fill regression");
            var mode=RenderSettings.ambientMode;var sky=RenderSettings.ambientSkyColor;var equator=RenderSettings.ambientEquatorColor;var ground=RenderSettings.ambientGroundColor;
            bool fog=RenderSettings.fog;var fogColor=RenderSettings.fogColor;var fogMode=RenderSettings.fogMode;
            try{
                StadiumLighting.Apply(root.transform,true);
                Assert.AreEqual(1,root.GetComponentsInChildren<Light>().Length,"No additional realtime lighting pass");
                Assert.AreEqual(StadiumLighting.NightAmbient,RenderSettings.ambientSkyColor,"Pitch top/sky are not globally brightened");
                Assert.Greater(RenderSettings.ambientEquatorColor.linear.grayscale,.16f,"Dark sides of kits need floodlight spill");
                Assert.Less(RenderSettings.ambientEquatorColor.linear.grayscale,StadiumLighting.DayEquator.linear.grayscale,"Night remains distinct from daylight");
                Assert.AreEqual(StadiumLighting.NightSky,RenderSettings.fogColor);
            }finally{Object.DestroyImmediate(root);RenderSettings.ambientMode=mode;RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;RenderSettings.fog=fog;RenderSettings.fogColor=fogColor;RenderSettings.fogMode=fogMode;}
        }
        static int FirstDay(System.DayOfWeek wanted){for(int day=0;day<7;day++)if(Career.Epoch.AddDays(day).DayOfWeek==wanted)return day;throw new InvalidOperationException();}
        static float Area(Mesh mesh){var v=mesh.vertices;var t=mesh.triangles;float area=0;for(int i=0;i<t.Length;i+=3)area+=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).magnitude*.5f;return area;}

        [Test] public void PreferenceOverridesTheFixture()
        {
            var evening=new Fixture{date="2027-02-20T20:00Z"};
            Assert.IsTrue(StadiumLighting.Floodlit(StadiumLighting.Auto,evening));
            Assert.IsFalse(StadiumLighting.Floodlit(StadiumLighting.Day,evening));
            Assert.IsTrue(StadiumLighting.Floodlit(StadiumLighting.Night,null));
            Assert.IsFalse(StadiumLighting.Floodlit(StadiumLighting.Auto,null),"Sans match de calendrier : après-midi");
        }
        [TestCase("2027-02-20T20:00Z",true)]  // soirée d'hiver
        [TestCase("2027-04-10T14:00Z",false)] // après-midi de printemps
        [TestCase("2026-08-15T19:00Z",true)]  // fin de match après le coucher en août
        [TestCase("2026-08-15T15:00Z",false)]
        public void KickoffTimeDecidesFloodlights(string date,bool expected)=>Assert.AreEqual(expected,StadiumLighting.Floodlit(StadiumLighting.Auto,new Fixture{date=date}));
        [Test] public void WithoutKickoffTimeWeekdaysAndKnockoutsAreEvenings()
        {
            int saturday=FirstDay(System.DayOfWeek.Saturday),sunday=FirstDay(System.DayOfWeek.Sunday),wednesday=FirstDay(System.DayOfWeek.Wednesday);
            Assert.IsFalse(StadiumLighting.Floodlit(StadiumLighting.Auto,new Fixture{day=saturday}));
            Assert.IsFalse(StadiumLighting.Floodlit(StadiumLighting.Auto,new Fixture{day=sunday+7,date="2026-07-05"}),"Date sans heure : règle du jour de la semaine");
            Assert.IsTrue(StadiumLighting.Floodlit(StadiumLighting.Auto,new Fixture{day=wednesday}));
            Assert.IsTrue(StadiumLighting.Floodlit(StadiumLighting.Auto,new Fixture{day=saturday,knockout=true}));
            Assert.AreEqual(StadiumLighting.Floodlit(StadiumLighting.Auto,new Fixture{day=wednesday+14}),StadiumLighting.Floodlit(StadiumLighting.Auto,new Fixture{day=wednesday+14}),"Règle déterministe");
        }
        [Test] public void RoofShadowCoversTheFarPartOfThePitch()
        {
            float edge=StadiumLighting.RoofShadowEdge(),endX=StadiumLighting.RoofShadowEndX();
            Assert.That(edge,Is.InRange(-30f,-15f),"Bande d'ombre visible mais pas plus de la moitié du terrain");
            Assert.That(endX,Is.InRange(35f,52.5f),"Le soleil de biais laisse le coin éloigné au soleil");
            // Le bord de l'ombre est bien aligné avec le bord avant du toit, dans l'axe du soleil.
            var roofEdge=new Vector3(0,StadiumGeometry.RoofHeight+StadiumGeometry.RoofThickness*.5f,-StadiumGeometry.RoofFront);
            var towardSun=-(StadiumLighting.SunRotation*Vector3.forward);var ground=StadiumLighting.SunShadowOnGround(roofEdge);
            Assert.AreEqual(0,ground.y,1e-4f);Assert.AreEqual(edge,ground.z,1e-4f);
            Assert.Less(Vector3.Angle(roofEdge-ground,towardSun),.01f);
        }
        [Test] public void ShadeTintDarkensAndCoolsTheTurf()
        {
            var tint=StadiumLighting.ShadeTint();
            foreach(var channel in new[]{tint.r,tint.g,tint.b})Assert.That(channel,Is.InRange(.3f,.8f),"Ombre lisible, ni noire ni plus claire que l'ambiance");
            Assert.Greater(tint.b,tint.r,"L'ombre éclairée par le ciel tire vers le bleu");
        }
        [Test] public void FarStandSitsInTheRoofShadowOnlyByDay()
        {
            Color day=StadiumLighting.FarStandShade(false);
            Assert.AreEqual(Color.white,StadiumLighting.FarStandShade(true),"Le soir, pas d'ombre du toit");
            Assert.AreEqual(1f,day.a);
            // Plus sombre qu'au soleil, modérément : les contremarches vues de la caméra sont déjà à contre-jour.
            foreach(var channel in new[]{day.r,day.g,day.b})Assert.That(channel,Is.InRange(.4f,.85f));
            Assert.Greater(day.b,day.r,"Ombre éclairée par le ciel : teinte froide");
        }
        [Test] public void ShadedSurfacesUseAnUnlitMaterialIncludedInTheBuild()
        {
            // Lit assombri gardait le reflet rasant du soleil (pelouse grise) : Unlit, chargé depuis Resources.
            var template=Resources.Load<Material>(StadiumLighting.ShadeMaterialPath);
            Assert.IsNotNull(template,"Matériau d'ombre absent de Resources : shader Unlit exclu de l'APK");
            Assert.AreEqual("Universal Render Pipeline/Unlit",template.shader.name);
            var turf=new Color(.13f,.36f,.15f);var tint=StadiumLighting.ShadeTint();var material=StadiumLighting.ShadedMaterial(turf);
            try{
                Assert.AreEqual(template.shader,material.shader);Assert.AreNotSame(template,material);
                Assert.AreEqual(turf.g*tint.g,material.color.g,1e-4f);Assert.AreEqual(1f,material.color.a);
            }finally{Object.DestroyImmediate(material);}
        }
        [Test] public void LedBoardsAndLitLampsDoNotDependOnTheLight()
        {
            var unlit=Resources.Load<Material>(StadiumLighting.ShadeMaterialPath).shader;var atlas=new Texture2D(4,4);
            Material day=StadiumLighting.BoardMaterial(false,atlas),night=StadiumLighting.BoardMaterial(true,atlas),off=StadiumLighting.LampMaterial(false),on=StadiumLighting.LampMaterial(true);
            try{
                Assert.AreEqual(unlit,day.shader);Assert.AreEqual(unlit,night.shader);Assert.AreSame(atlas,day.mainTexture);
                Assert.LessOrEqual(day.color.r,night.color.r,"Panneaux un peu moins vifs en plein jour");
                Assert.AreEqual(unlit,on.shader,"Lampes allumées le soir");Assert.AreNotEqual(unlit,off.shader,"Lampes éteintes éclairées par le soleil");
            }finally{foreach(var m in new[]{day,night,off,on})Object.DestroyImmediate(m);Object.DestroyImmediate(atlas);}
        }
        [Test] public void LitAndShadedTurfTileThePitchExactly()
        {
            float edge=StadiumLighting.RoofShadowEdge(),endX=StadiumLighting.RoofShadowEndX();
            Mesh lit=PitchTurf.Surface(false,edge,endX),shaded=PitchTurf.Surface(true,edge,endX),whole=PitchTurf.Surface(false);
            try{
                Assert.AreEqual(105*68,Area(whole),.5f);Assert.AreEqual(105*68,Area(lit)+Area(shaded),.5f);
                Assert.AreEqual((endX+52.5f)*(edge+34),Area(shaded),.5f);
                foreach(var v in shaded.vertices){Assert.LessOrEqual(v.z,edge+1e-3f);Assert.LessOrEqual(v.x,endX+1e-3f);}
                Assert.AreEqual(2,lit.subMeshCount);Assert.AreEqual(2,shaded.subMeshCount);Assert.AreEqual(shaded.vertexCount,shaded.uv.Length);
                foreach(var n in lit.normals)Assert.Greater(n.y,.99f);
            }finally{Object.DestroyImmediate(lit);Object.DestroyImmediate(shaded);Object.DestroyImmediate(whole);}
        }
        [Test] public void MarkingsSplitAtTheShadowEdgeWithoutLosingLines()
        {
            float edge=StadiumLighting.RoofShadowEdge(),endX=StadiumLighting.RoofShadowEndX();
            Mesh lit=StadiumGeometry.PitchMarkings(false,edge,endX),shaded=StadiumGeometry.PitchMarkings(true,edge,endX),whole=StadiumGeometry.PitchMarkings();
            try{
                Assert.Greater(shaded.vertexCount,0);Assert.AreEqual(Area(whole),Area(lit)+Area(shaded),.05f);
                foreach(var v in shaded.vertices){Assert.LessOrEqual(v.z,edge+.06f);Assert.LessOrEqual(v.x,endX+.06f);}
            }finally{Object.DestroyImmediate(lit);Object.DestroyImmediate(shaded);Object.DestroyImmediate(whole);}
        }
        [Test] public void AdvertisingBoardsStayOffThePitchAndAlternateClubs()
        {
            var mesh=StadiumGeometry.PerimeterBoards();
            try{
                Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);Assert.Less(mesh.vertexCount,2000);
                foreach(var v in mesh.vertices){Assert.IsTrue(Mathf.Abs(v.x)>54||Mathf.Abs(v.z)>35.5f,"Panneau sur le terrain : "+v);Assert.LessOrEqual(v.y,StadiumGeometry.BoardBase+StadiumGeometry.BoardHeight+1e-3f);}
                foreach(var uv in mesh.uv)Assert.That(uv.y,Is.InRange(0f,1f));
            }finally{Object.DestroyImmediate(mesh);}
            for(int run=0;run<4;run++)for(int panel=0;panel<StadiumGeometry.SidePanels;panel++)
                Assert.AreEqual(panel%2==1,StadiumGeometry.BoardDesign(run,panel)>=StadiumAtmosphere.BoardDesignsPerClub,"Recevant et visiteur alternent");
        }
        [Test] public void BoardAtlasUsesClubColoursInsideADarkFrame()
        {
            Color home=new Color(.8f,.1f,.1f),away=new Color(.1f,.2f,.8f);
            for(int design=0;design<StadiumAtmosphere.BoardDesigns;design++){
                Assert.AreEqual(StadiumAtmosphere.BoardFrame,StadiumAtmosphere.BoardPixel(design,10,0,home,away));
                Assert.AreEqual(StadiumAtmosphere.BoardFrame,StadiumAtmosphere.BoardPixel(design,10,StadiumAtmosphere.BoardRowPixels-1,home,away));
                var club=design<StadiumAtmosphere.BoardDesignsPerClub?home:away;int count=0;
                for(int x=0;x<StadiumAtmosphere.BoardAtlasWidth;x++)for(int y=0;y<StadiumAtmosphere.BoardRowPixels;y++)if(StadiumAtmosphere.BoardPixel(design,x,y,home,away)==club)count++;
                Assert.Greater(count,StadiumAtmosphere.BoardAtlasWidth*StadiumAtmosphere.BoardRowPixels/8,"Motif "+design+" aux couleurs de son club");
            }
            var texture=StadiumAtmosphere.BoardAtlas(home,away);
            try{Assert.AreEqual(StadiumAtmosphere.BoardAtlasWidth,texture.width);Assert.AreEqual(StadiumAtmosphere.BoardAtlasHeight,texture.height);}
            finally{Object.DestroyImmediate(texture);}
        }
        [Test] public void FloodlightsStandOutsideThePlayingArea()
        {
            Mesh masts=StadiumGeometry.FloodlightMasts(),lamps=StadiumGeometry.FloodlightLamps();
            try{
                foreach(var v in masts.vertices)Assert.Greater(Mathf.Abs(v.x),70);
                foreach(var v in lamps.vertices){Assert.Greater(v.y,15);Assert.IsTrue(Mathf.Abs(v.x)>70||v.z<-StadiumGeometry.RoofFront+.5f);}
                Assert.Less(masts.vertexCount+lamps.vertexCount,1500);
            }finally{Object.DestroyImmediate(masts);Object.DestroyImmediate(lamps);}
        }
    }
}
