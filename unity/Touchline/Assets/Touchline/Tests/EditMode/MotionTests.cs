using NUnit.Framework;
using System.Linq;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class MotionTests
    {
        [Test] public void ImportedStaturesKeepProvenanceAndUnknownsRemainEmpty(){var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);var scoutingLeagues=db.leagues.Where(l=>l.scoutingOnly).Select(l=>l.id).ToArray();var scoutingClubs=db.clubs.Where(c=>scoutingLeagues.Contains(c.league)).Select(c=>c.id).ToArray();var original=db.players.Where(p=>!scoutingClubs.Contains(p.team)).ToArray();var worldwide=db.players.Where(p=>scoutingClubs.Contains(p.team)).ToArray();Assert.AreEqual(9098,original.Count(p=>p.heightCm>0));Assert.AreEqual(7079,worldwide.Count(p=>p.heightCm>0));Assert.AreEqual(16177,db.players.Count(p=>p.heightCm>0));Assert.AreEqual(7973,db.players.Count(p=>p.weightKg>0));Assert.IsTrue(worldwide.All(p=>p.weightKg==0),"Missing worldwide weights must not be invented.");foreach(var p in db.players.Where(p=>p.heightCm>0)){Assert.That(p.heightCm,Is.InRange(145,215));if(scoutingClubs.Contains(p.team)){Assert.IsTrue(p.rosterSource.StartsWith("https://site.api.espn.com/"));StringAssert.Contains("ESPN",p.physiqueSource);Assert.AreEqual("2026-10-05",p.rosterAsOf);}else Assert.IsTrue(p.physiqueSource.StartsWith("https://site.api.espn.com/"));}Assert.IsTrue(db.players.Any(p=>p.heightCm==0));}
        [TestCase("421406")][TestCase("382832")] public void ImpossibleProviderHeightUnitsRemainUnknownInsteadOfInvented(string id){var db=JsonUtility.FromJson<Database>(Resources.Load<TextAsset>("Data/database").text);var p=db.Find(id);Assert.IsNotNull(p);Assert.AreEqual(0,p.heightCm);StringAssert.Contains("taille incohérente écartée",p.physiqueSource);Assert.IsTrue(p.rosterSource.StartsWith("https://site.api.espn.com/"));}
        [Test] public void PlayerHeightScalesTheSameSkeletonAndUpdatesForSubstitute()
        {
            var go=new GameObject("Real stature");var view=go.AddComponent<PlayerView>();var source=JsonUtility.FromJson<HumanSource>(Resources.Load<TextAsset>("Models/footballer").text);view.Build(new PlayerData{id="short",heightCm=166},0,9,Color.white);
            try{Assert.That(go.transform.localScale.y*source.height,Is.EqualTo(1.66f).Within(.001));var mesh=go.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;view.ChangeIdentity(new PlayerData{id="tall",heightCm=202});Assert.That(go.transform.localScale.y*source.height,Is.EqualTo(2.02f).Within(.001));Assert.AreSame(mesh,go.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh);view.ChangeIdentity(new PlayerData{id="unknown",heightCm=0});Assert.AreEqual(Vector3.one,go.transform.localScale);}
            finally{Object.DestroyImmediate(go);}
        }
        [TestCase(160)] [TestCase(205)] public void DifferentHeightsKeepFeetGroundedAndKickContact(int height)
        {
            var go=new GameObject("Stature contact");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="height-test",heightCm=height},0,9,Color.white);
            try{var actor=new Actor{slot=9,action="kick",actionKind="pass",actionTime=.46f,actionSequence=1,actionTarget=new Point(0,.42f),actionHeight=.11f};view.Render(actor,1,1f/60,new Vector3(0,.11f,.42f));
                var feet=go.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("foot.")).ToArray();Assert.AreEqual(2,feet.Length);foreach(var foot in feet)Assert.GreaterOrEqual(foot.position.y,.045f);Assert.Less(Vector3.Distance(view.InsideFootContactPosition(false),new Vector3(0,.11f,.42f)),.045f);}
            finally{Object.DestroyImmediate(go);}
        }
        [Test] public void SettledWallFacesBallAndProtectsBody()
        {
            var go=new GameObject("Wall defender");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="wall"},1,2,Color.blue);
            var actor=new Actor{intent="wall",action="idle",position=new Point(40,0),previous=new Point(40,0),angle=0};var ball=new Vector3(30,.11f,0);
            try{view.Render(actor,1,1,ball);Assert.Greater(Vector3.Dot(go.transform.forward,(ball-go.transform.position).normalized),.99f);var hands=go.transform.InverseTransformPoint(view.HeldBallPosition);Assert.That(hands.y,Is.InRange(.85f,1.15f));Assert.That(hands.z,Is.InRange(.15f,.45f));}
            finally{Object.DestroyImmediate(go);}
        }
        [Test] public void MergedAnatomyPreservesSectionsAndSkinning()
        {
            var go=new GameObject("Merged anatomy");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="merged"},0,9,Color.white);
            try{var renderers=go.GetComponentsInChildren<SkinnedMeshRenderer>();Assert.AreEqual(1,renderers.Length);var mesh=renderers[0].sharedMesh;var source=JsonUtility.FromJson<HumanSource>(Resources.Load<TextAsset>("Models/footballer").text);int expected=0;
                Assert.AreEqual(source.parts.Length+1,mesh.subMeshCount,"The shirt print is a separate skinned section.");Assert.AreEqual(mesh.subMeshCount,renderers[0].sharedMaterials.Length);Assert.AreEqual(source.bones.Length,mesh.bindposes.Length);
                for(int i=0;i<source.parts.Length;i++){var indices=mesh.GetTriangles(i);Assert.AreEqual(source.parts[i].index.Length,indices.Length);for(int j=0;j<indices.Length;j++)Assert.AreEqual(source.parts[i].index[j]+expected,indices[j]);expected+=source.parts[i].position.Length/3;}
                Assert.Greater(mesh.vertexCount,expected,"The back print must have its own vertices.");
                var print=mesh.GetTriangles(source.parts.Length);Assert.Greater(print.Length,0);foreach(int index in print)Assert.That(index,Is.InRange(expected,mesh.vertexCount-1));
                for(int i=expected;i<mesh.vertexCount;i++){Assert.Less(mesh.normals[i].z,0,"Print faces the back of the shirt.");Assert.That(mesh.uv[i].x,Is.InRange(0,1));Assert.That(mesh.uv[i].y,Is.InRange(0,1));}
                foreach(var weight in mesh.boneWeights){Assert.That(weight.weight0+weight.weight1+weight.weight2+weight.weight3,Is.EqualTo(1).Within(.001));Assert.That(weight.boneIndex0,Is.InRange(0,source.bones.Length-1));}
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void DiveArmsFollowTheContactHeightInsteadOfOneFixedPose()
        {
            var go=new GameObject("Dive target");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="keeper"},0,0,Color.yellow);var actor=new Actor{action="dive",actionTime=.85f,diveSide=1,actionSequence=1,actionTarget=new Point(.85f,.3f)};
            try{actor.actionHeight=.3f;view.Render(actor,1,1f/60);var low=view.HeldBallPosition;actor.actionHeight=1.25f;view.Render(actor,1,1f/60);Assert.Greater(view.HeldBallPosition.y-low.y,.25f);}
            finally{Object.DestroyImmediate(go);}
        }
        [Test] public void ShirtPrintDeformsWithTheTorsoInsteadOfFloatingOverTheAnimatedKit()
        {
            var go=new GameObject("Skinned print motion");var before=new Mesh();var after=new Mesh();
            try{
                var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="print-motion"},0,9,Color.white);
                var renderer=go.GetComponentInChildren<SkinnedMeshRenderer>();var print=renderer.sharedMesh.GetTriangles(renderer.sharedMesh.subMeshCount-1).Distinct().ToArray();
                renderer.BakeMesh(before);var spine=go.GetComponentsInChildren<Transform>().First(t=>t.name=="spine05");spine.localRotation=Quaternion.Euler(20,0,0);renderer.BakeMesh(after);
                float movement=0;foreach(int index in print){var delta=after.vertices[index]-before.vertices[index];Assert.IsFalse(float.IsNaN(delta.sqrMagnitude));movement=Mathf.Max(movement,delta.magnitude);}
                Assert.Greater(movement,.02f,"The print must be skinned to the rotating torso, not a rigid badge.");
            }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(before);Object.DestroyImmediate(after);}
        }
        [TestCase("dive",.98f,"wrist.R")]
        [TestCase("claim",.55f,"wrist.R")]
        [TestCase("throw",.9f,"upperarm01.L")]
        public void ContactPosesMoveBetweenSimulationSteps(string action,float remaining,string boneName)
        {
            var go=new GameObject("Interpolated contact");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="keeper"},0,0,Color.yellow);
            var actor=new Actor{action=action,actionTime=remaining,diveSide=1};Transform bone=null;foreach(var t in go.GetComponentsInChildren<Transform>())if(t.name==boneName)bone=t;
            try{Assert.NotNull(bone);var positions=new Vector3[3];for(int i=0;i<3;i++){view.Render(actor,i*.5f,1f/60,new Vector3(0,action=="throw"?Mathf.Lerp(.3f,.64f,i*.5f):.3f,.5f));positions[i]=bone.position;}
                Assert.Greater(Vector3.Distance(positions[0],positions[1]),.001f);Assert.Greater(Vector3.Distance(positions[1],positions[2]),.001f);Assert.AreEqual(remaining,actor.actionTime,"Rendering must not advance simulation state");}
            finally{Object.DestroyImmediate(go);}
        }
        [Test] public void FootPlacementRemainsAboveThePitchAcrossFrameRates()
        {
            foreach(int fps in new[]{30,60,120}){
                var go=new GameObject("Motion test");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="test",preferredFoot="Right"},0,9,Color.white);
                var actor=new Actor{side=0,slot=9,velocity=new Point(0,4),action="run"};float dt=1f/fps;
                try{for(int frame=0;frame<fps*3;frame++){actor.previous=actor.position;actor.position+=actor.velocity*dt;actor.stride+=4*dt;view.Render(actor,1,dt,new Vector3(0,.11f,20));foreach(bool left in new[]{false,true}){var foot=view.FootPosition(left);Assert.Greater(foot.y,-.025f);Assert.Less(foot.y,.50f);Assert.IsFalse(float.IsNaN(foot.y));}}}
                finally{Object.DestroyImmediate(go);}
            }
        }
        [Test] public void KeeperGatherKeepsHandsTogetherAndFeetGrounded()
        {
            var go=new GameObject("Gather test");var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="keeper"},0,0,Color.yellow);var actor=new Actor{action="claim",actionTime=.8f};
            try{for(int i=0;i<45;i++){actor.actionTime=.8f-i/60f;view.Render(actor,1,1f/60,new Vector3(0,.3f,.5f));Assert.That(view.HeldBallPosition.y,Is.InRange(.2f,1.5f));Assert.Greater(view.FootPosition(true).y,-.025f);Assert.Greater(view.FootPosition(false).y,-.025f);}}
            finally{Object.DestroyImmediate(go);}
        }
        [Test] public void KeeperDiveHasTwoDistinctDirections()
        {
            var directions=new Vector3[2];
            for(int i=0;i<2;i++){
                var go=new GameObject("Independent keeper dive");try{
                    var view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id="keeper"},0,0,Color.yellow);
                    view.Render(new Actor{slot=0,action="dive",actionTime=1f,diveSide=i==0?1:-1},1,.016f);
                    var animator=go.GetComponentInChildren<Animator>();var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var chest=animator.GetBoneTransform(HumanBodyBones.Chest);directions[i]=(chest.position-hips.position).normalized;
                    foreach(var bone in go.GetComponentsInChildren<Transform>())if(bone.name=="head"||bone.name.StartsWith("foot."))Assert.Greater(bone.position.y,-.025f,bone.name);
                }finally{Object.DestroyImmediate(go);}
            }
            Assert.Greater(Vector3.Angle(directions[0],directions[1]),90);Assert.Less(directions[0].x*directions[1].x,0,"The actual torso must lean toward opposite sides.");
        }
    }
}
