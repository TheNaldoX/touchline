using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class PlayerAppearanceTests
    {
        [Test] public void TexturedAnatomyHasCompleteCoordinatesAndHeadBoundEyes()
        {
            var source=JsonUtility.FromJson<HumanSource>(Resources.Load<TextAsset>("Models/footballer").text);bool eyes=false;
            foreach(var part in source.parts){
                int count=part.position.Length/3;Assert.AreEqual(count*2,part.uv.Length);Assert.AreEqual(count*3,part.normal.Length);
                foreach(float uv in part.uv)Assert.IsFalse(float.IsNaN(uv)||float.IsInfinity(uv));
                for(int i=0;i<count;i++)Assert.That(new Vector3(part.normal[i*3],part.normal[i*3+1],part.normal[i*3+2]).magnitude,Is.EqualTo(1).Within(.01f));
                if(part.material==6)eyes=true;
                if(part.material==6||part.material==7)for(int i=0;i<count;i++){Assert.AreEqual("head",source.bones[part.skinIndex[i*4]].name);Assert.AreEqual(1,part.skinWeight[i*4]);}
            }
            Assert.IsTrue(eyes);
        }
        [Test] public void AppearanceSurvivesSubstitutionWithoutAddingRenderers()
        {
            var go=new GameObject("Appearance substitution");try{
                var view=go.AddComponent<PlayerView>();var player=new PlayerData{id="appearance-a",heightCm=180};view.Build(player,0,9,Color.white);
                var renderer=go.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=renderer.sharedMesh;var skin=renderer.sharedMaterials[0].mainTexture;
                Assert.IsNotNull(skin);Assert.IsNotNull(renderer.sharedMaterials[renderer.sharedMaterials.Length-1].mainTexture);Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);
                view.ChangeIdentity(new PlayerData{id="appearance-b",heightCm=195});view.ChangeIdentity(player);
                Assert.AreSame(skin,renderer.sharedMaterials[0].mainTexture);Assert.AreSame(mesh,renderer.sharedMesh);Assert.AreEqual(1,go.GetComponentsInChildren<SkinnedMeshRenderer>().Length);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
