using NUnit.Framework;
using Touchline.Core;
using UnityEngine;

namespace Touchline.Tests
{
    public class KeeperAppearanceTests
    {
        [Test] public void GlovesShareTheFingerSkeletonAndStayAroundBothHands()
        {
            var go=new GameObject("Keeper glove skinning");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="glove-keeper"},0,0,Color.blue);
                var glove=go.transform.Find("Rig/Keeper gloves").GetComponent<SkinnedMeshRenderer>();var body=go.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=glove.sharedMesh;
                Assert.IsTrue(glove.enabled);Assert.AreEqual(2,mesh.subMeshCount);Assert.Greater(mesh.vertexCount,100);Assert.Less(mesh.vertexCount,10000);Assert.AreEqual(body.bones.Length,glove.bones.Length);
                Vector3 left=default,right=default;int fingers=0;
                for(int i=0;i<glove.bones.Length;i++){Assert.AreSame(body.bones[i],glove.bones[i]);if(glove.bones[i].name=="wrist.L")left=glove.bones[i].position;if(glove.bones[i].name=="wrist.R")right=glove.bones[i].position;}
                foreach(var p in mesh.vertices)Assert.Less(Mathf.Min(Vector3.Distance(p,left),Vector3.Distance(p,right)),.30f,"Glove geometry must not include body surfaces");
                var weighted=new bool[glove.bones.Length];foreach(var w in mesh.boneWeights){Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.001f));if(w.weight0>.1f)weighted[w.boneIndex0]=true;if(w.weight1>.1f)weighted[w.boneIndex1]=true;}
                for(int i=0;i<weighted.Length;i++)if(weighted[i]&&glove.bones[i].name.StartsWith("finger"))fingers++;
                Assert.Greater(fingers,20,"Individual fingers must deform the glove");
                TestContext.WriteLine("Gloves: "+mesh.vertexCount+" vertices, "+mesh.triangles.Length/3+" triangles");
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void PositionChangesReuseGlovesAndRestoreTeamShirt()
        {
            var go=new GameObject("Keeper position changes");try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="emergency-keeper"},0,9,Color.blue);
                var body=go.GetComponentInChildren<SkinnedMeshRenderer>();Material shirt=null;foreach(var material in body.sharedMaterials)if(material.color==Color.blue)shirt=material;Assert.IsNotNull(shirt);
                Assert.IsNull(go.transform.Find("Rig/Keeper gloves"));var actor=new Actor{slot=0,action="idle"};view.Render(actor,1,1f/60);
                var gloves=go.transform.Find("Rig/Keeper gloves").GetComponent<SkinnedMeshRenderer>();var mesh=gloves.sharedMesh;Assert.IsTrue(gloves.enabled);Assert.AreNotEqual(Color.blue,shirt.color);
                actor.slot=9;view.Render(actor,1,1f/60);Assert.IsFalse(gloves.enabled);Assert.AreEqual(Color.blue,shirt.color);
                actor.slot=0;view.ChangeIdentity(new PlayerData{id="replacement-keeper",heightCm=194});view.Render(actor,1,1f/60);Assert.IsTrue(gloves.enabled);Assert.AreSame(mesh,gloves.sharedMesh);Assert.AreEqual(2,go.GetComponentsInChildren<SkinnedMeshRenderer>().Length);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
