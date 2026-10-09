using System.Collections.Generic;
using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class PlayerKitTests
    {
        [Test] public void FabricDetailPreservesKitColoursAndIsSpatiallyStable()
        {
            float min=1,max=0;
            for(int i=0;i<100;i++){
                var p=new Vector3(-.2f+i*.004f,1.31f,.08f);float v=KitTexture.FabricShade(p);
                Assert.That(v,Is.InRange(.88f,1f));Assert.AreEqual(v,KitTexture.FabricShade(p));min=Mathf.Min(min,v);max=Mathf.Max(max,v);
            }
            Assert.Greater(max-min,.02f,"Fabric variation preserves team colours");
        }

        [Test] public void ClubKitsAreDeterministicAndKeepersStandApart()
        {
            var home=new Color(.80f,.08f,.10f);var away=new Color(.05f,.09f,.22f);
            var first=MatchKit.ForMatch("club-a",home,"club-b",away,away);var second=MatchKit.ForMatch("club-a",home,"club-b",away,away);
            for(int side=0;side<2;side++){Assert.AreEqual(first[side].pattern,second[side].pattern);Assert.AreEqual(first[side].trim,second[side].trim);Assert.AreEqual(first[side].keeper.shirt,second[side].keeper.shirt);}
            Assert.AreEqual(home,first[0].shirt);Assert.AreEqual(away,first[1].shirt);
            Assert.Greater(MatchKit.Distance(first[0].keeper.shirt,first[1].keeper.shirt),.3f,"Deux gardiens différents");
            foreach(var kit in first)foreach(var outfield in first)Assert.Greater(MatchKit.Distance(kit.keeper.shirt,outfield.shirt),.3f,"Gardien distinct des joueurs de champ");
            var patterns=new HashSet<KitPattern>();for(int i=0;i<60;i++)patterns.Add(MatchKit.ForMatch("club"+i,home,"other",away,away)[0].pattern);
            Assert.GreaterOrEqual(patterns.Count,5,"Plusieurs motifs selon le club");
            foreach(var kit in first)Assert.Greater(MatchKitPalette.Contrast(kit.printFill,kit.pattern==KitPattern.CentreBand?kit.trim:kit.shirt),2f,"Flocage lisible");
        }
        [Test] public void PatternsFollowTheBodyNotTheTextureSeams()
        {
            Assert.AreEqual(0,KitTexture.Secondary(KitPattern.Stripes,new Vector3(0,1.3f,-.09f),false),.01f,"Rayure centrale principale");
            Assert.AreEqual(1,KitTexture.Secondary(KitPattern.Stripes,new Vector3(.055f,1.3f,.1f),false),.01f);
            Assert.AreEqual(KitTexture.Secondary(KitPattern.Stripes,new Vector3(.055f,1.3f,.1f),false),KitTexture.Secondary(KitPattern.Stripes,new Vector3(.055f,1.3f,-.1f),false),"Devant et dos raccordés");
            Assert.AreEqual(1,KitTexture.Secondary(KitPattern.Sleeves,new Vector3(.3f,1.4f,0),true));Assert.AreEqual(0,KitTexture.Secondary(KitPattern.Sleeves,Vector3.up*1.3f,false));
            Assert.AreEqual(0,KitTexture.Secondary(KitPattern.Plain,new Vector3(.1f,1.2f,.1f),false));
        }
        [Test] public void LetteringIsUppercaseWithoutAccents()
        {
            Assert.AreEqual("HOJBJERG",ShirtLettering.Surname("Pierre-Emile Højbjerg"));Assert.AreEqual("DE LANGE",ShirtLettering.Surname("Jeffrey de Lange"));
            Assert.AreEqual("MEITE",ShirtLettering.Printable("Meïté"));Assert.AreEqual("MARQUINHOS",ShirtLettering.Surname("Marquinhos"));
            foreach(char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")Assert.IsNotNull(ShirtLettering.Glyph(c).strokes,c.ToString());
        }
        [Test] public void BackPrintIsSkinnedToTheShirtAndSharesAtlasCells()
        {
            var a=new GameObject("Print a");var b=new GameObject("Print b");try{
                var data=new PlayerData{id="print-a",name="Amine Gouiri",number=9,heightCm=180};
                var first=a.AddComponent<PlayerView>();first.Build(data,0,9,new Color(.1f,.2f,.8f));
                var second=b.AddComponent<PlayerView>();second.Build(new PlayerData{id="print-b",name="Amine Gouiri",number=9,heightCm=181},0,9,new Color(.1f,.2f,.8f));
                Assert.GreaterOrEqual(first.PrintCell,0);Assert.AreEqual(first.PrintCell,second.PrintCell,"Même flocage, même case");
                var renderer=a.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=renderer.sharedMesh;
                Assert.AreEqual(mesh.subMeshCount,renderer.sharedMaterials.Length);var print=mesh.GetTriangles(mesh.subMeshCount-1);Assert.Greater(print.Length,0);
                var vertices=mesh.vertices;var weights=mesh.boneWeights;
                foreach(int i in print){Assert.Less(vertices[i].z,0,"Le flocage est au dos");Assert.That(vertices[i].y,Is.InRange(ShirtPrint.Bottom-.01f,ShirtPrint.Top+.01f));
                    Assert.That(weights[i].weight0+weights[i].weight1+weights[i].weight2+weights[i].weight3,Is.EqualTo(1).Within(.001f));}
                int cell=first.PrintCell;first.ChangeIdentity(new PlayerData{id="print-c",name="Neal Maupay",number=11,heightCm=172});
                Assert.AreNotEqual(cell,first.PrintCell,"Le remplaçant a son propre flocage");Assert.AreEqual(1,ShirtPrint.Users(cell));
            }finally{Object.DestroyImmediate(a);Object.DestroyImmediate(b);}
        }
        [Test] public void HairAndBootsVaryButStayFixedPerPlayer()
        {
            var hair=new HashSet<Color>();var boots=new HashSet<Color>();
            for(uint id=0;id<400;id++){hair.Add(PlayerView.HairColour(id*2654435761u));boots.Add(PlayerView.BootColour(id*2654435761u));}
            var styles=new HashSet<int>();for(uint id=0;id<400;id++)styles.Add(PlayerView.HairStyle(id*2654435761u));
            Assert.GreaterOrEqual(hair.Count,6);Assert.GreaterOrEqual(boots.Count,6);Assert.AreEqual(3,styles.Count,"Trois coupes");
            Assert.AreEqual(PlayerView.HairColour(12345u),PlayerView.HairColour(12345u));
        }
        [TestCase(-1)] [TestCase(1)] public void GoalNetBulgesUnderTheBallThenSettles(int side)
        {
            var mesh=StadiumGeometry.GoalNet(side);try{
                var rest=mesh.vertices;var ripple=new GoalNetRipple(mesh,side);var contact=new Vector3(side*54f,1,0);
                ripple.Advance(new Vector3(side*52.9f,1,0),true,1f/30);ripple.Advance(new Vector3(side*53.6f,1,0),true,1f/30); // 21 m/s dans le filet
                for(int i=0;i<4;i++)ripple.Advance(contact,true,1f/30);
                float near=0,far=0;var moved=mesh.vertices;
                for(int i=0;i<rest.Length;i++){float d=(moved[i]-rest[i]).magnitude;if((rest[i]-contact).magnitude<.5f)near=Mathf.Max(near,d);if(Mathf.Abs(rest[i].z)>3f)far=Mathf.Max(far,d);}
                Assert.Greater(near,.02f,"Le filet se creuse sous le ballon");Assert.AreEqual(0,far,1e-5f,"Le reste du filet ne bouge pas");
                TestContext.WriteLine($"But {side} : déplacement central {near:0.0000} m, distant {far:0.0000} m");
                for(int i=0;i<150;i++)ripple.Advance(contact,false,1f/30);
                moved=mesh.vertices;for(int i=0;i<rest.Length;i++)Assert.AreEqual(0,(moved[i]-rest[i]).magnitude,1e-4f);
            }finally{Object.DestroyImmediate(mesh);}
        }
        [TestCase(-1)] [TestCase(1)] public void NetThreadsHaveLocalDeformationVerticesWithinMobileBudget(int side)
        {
            var mesh=StadiumGeometry.GoalNet(side);try{
                // Per goal budget: shared rings, one renderer/material; no capped mini-beams.
                Assert.LessOrEqual(mesh.vertexCount,4500);Assert.LessOrEqual(mesh.triangles.Length/3,7500);
                TestContext.WriteLine($"But {side} : {mesh.vertexCount} sommets, {mesh.triangles.Length/3} triangles");
                var vertices=mesh.vertices;var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)for(int edge=0;edge<3;edge++)
                    Assert.Less(Vector3.Distance(vertices[triangles[i+edge]],vertices[triangles[i+(edge+1)%3]]),.35f,"Pas de fil long sans sommet intermédiaire près du contact");
            }finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void PitchWearSitsBetweenTurfAndLinesAndFadesOut()
        {
            var mesh=PitchTurf.WearPatches();try{
                Assert.AreEqual(12,mesh.vertexCount);
                foreach(var v in mesh.vertices){Assert.That(v.y,Is.InRange(.001f,.02f),"Sous les lignes, au-dessus du gazon");Assert.LessOrEqual(Mathf.Abs(v.x),PitchTurf.HalfLength);Assert.LessOrEqual(Mathf.Abs(v.z),PitchTurf.HalfWidth);}
                foreach(var n in mesh.normals)Assert.Greater(n.y,.99f);
            }finally{Object.DestroyImmediate(mesh);}
            int c=PitchTurf.WearSize/2,edge=PitchTurf.WearSize-1;
            Assert.Greater(PitchTurf.WearAmount(c,c),.2f,"Usure marquée au centre");
            Assert.AreEqual(0,PitchTurf.WearAmount(0,0),.02f);Assert.AreEqual(0,PitchTurf.WearAmount(edge,c),.05f,"Bord fondu (pas de cadre visible)");
        }
        [Test] public void GradeIsSkippedInTheLowestQuality()
        {
            Assert.IsFalse(BroadcastGrade.Enabled(0));Assert.IsTrue(BroadcastGrade.Enabled(1));Assert.IsTrue(BroadcastGrade.Enabled(2));
        }
    }
}
