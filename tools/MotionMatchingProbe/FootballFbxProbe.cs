// Editor-only conversion check in the external project. No game import settings are changed.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MotionMatching;

public sealed class FootballProbeImport : AssetPostprocessor
{
    void OnPreprocessModel(){
        if(!assetPath.StartsWith("Assets/TouchlineStock/",StringComparison.Ordinal))return;
        var importer=(ModelImporter)assetImporter;
        importer.animationType=ModelImporterAnimationType.Legacy;
        importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
    }
}

public static class FootballFbxProbe
{
    const int Hz=60;
    static string Arg(string key){var args=Environment.GetCommandLineArgs();return args[Array.IndexOf(args,key)+1];}
    [Serializable] public class JointData{public string name;public int parent;public Vector3 offset;}
    [Serializable] public class FrameData{public Vector3 root;public Quaternion[] rotations;}
    [Serializable] public class Converted{public int hz=Hz;public JointData[] joints;public FrameData[] frames;}
    [Serializable] public class Result{public string clip;public int frames,joints;public float seconds,maxForwardKinematicsErrorMetres,maxRoundtripRotationDegrees,meanRootTravelSpeed,maxRootTravelSpeed,rootDistance,maxLoopJointAngleDegrees,maxLoopRootRelativePositionMetres;public string limitation="Raw FBX skeleton conversion, not Humanoid retargeting or validated cyclic motion-matching features. Loop metrics compare endpoints without blending.";}
    public static void Run(){
        string output=Arg("-footballOutput");var results=new List<Result>();
        foreach(string path in AssetDatabase.FindAssets("t:Model",new[]{"Assets/TouchlineStock"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal)){
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            var go=UnityEngine.Object.Instantiate(prefab);
            try{
                var root=go.GetComponentsInChildren<Transform>().First(t=>t.name.EndsWith("Hips",StringComparison.Ordinal));
                clip.SampleAnimation(go,0);
                var bones=root.GetComponentsInChildren<Transform>();var indices=bones.Select((b,i)=>(b,i)).ToDictionary(v=>v.b,v=>v.i);
                int count=Mathf.FloorToInt(clip.length*Hz)+1;
                var data=new Converted{joints=new JointData[bones.Length],frames=new FrameData[count]};
                for(int j=0;j<bones.Length;j++){
                    int parent=j==0?0:indices[bones[j].parent];
                    data.joints[j]=new JointData{name=bones[j].name,parent=parent,offset=j==0?Vector3.zero:Quaternion.Inverse(bones[parent].rotation)*(bones[j].position-bones[parent].position)};
                }
                var expected=new Vector3[count][];
                for(int f=0;f<count;f++){
                    clip.SampleAnimation(go,f/(float)Hz);
                    var rotations=new Quaternion[bones.Length];expected[f]=new Vector3[bones.Length];
                    for(int j=0;j<bones.Length;j++){
                        rotations[j]=j==0?bones[j].rotation:Quaternion.Inverse(bones[data.joints[j].parent].rotation)*bones[j].rotation;
                        expected[f][j]=bones[j].position;
                    }
                    data.frames[f]=new FrameData{root=bones[0].position,rotations=rotations};
                }
                string name=Path.GetFileNameWithoutExtension(path);
                string json=JsonUtility.ToJson(data);
                File.WriteAllText(Path.Combine(output,name+".motion.json"),json);
                var decoded=JsonUtility.FromJson<Converted>(json);
                var animation=new BVHAnimation();animation.SetFrameTime(1f/Hz);animation.InitFrames(count);
                for(int j=0;j<decoded.joints.Length;j++){var joint=decoded.joints[j];animation.AddJoint(new Skeleton.Joint(joint.name,j,joint.parent,joint.offset));}
                for(int f=0;f<count;f++)animation.AddFrame(f,new BVHAnimation.Frame(decoded.frames[f].root,decoded.frames[f].rotations));
                var holder=ScriptableObject.CreateInstance<AnimationData>();holder.SetAnimation(animation);
                if(!ReferenceEquals(holder.GetAnimation(),animation))throw new Exception("Upstream animation adapter mismatch");
                var result=new Result{clip=name,frames=count,joints=bones.Length,seconds=(count-1f)/Hz};
                var reconstructed=new Vector3[bones.Length];var worldRotations=new Quaternion[bones.Length];
                for(int f=0;f<count;f++){
                    for(int j=0;j<bones.Length;j++){
                        var joint=animation.Skeleton.Joints[j];var rotation=animation.Frames[f].LocalRotations[j];
                        worldRotations[j]=j==0?rotation:worldRotations[joint.ParentIndex]*rotation;
                        reconstructed[j]=j==0?animation.Frames[f].RootMotion:reconstructed[joint.ParentIndex]+worldRotations[joint.ParentIndex]*joint.LocalOffset;
                        result.maxForwardKinematicsErrorMetres=Mathf.Max(result.maxForwardKinematicsErrorMetres,Vector3.Distance(reconstructed[j],expected[f][j]));
                        result.maxRoundtripRotationDegrees=Mathf.Max(result.maxRoundtripRotationDegrees,Quaternion.Angle(data.frames[f].rotations[j],rotation));
                    }
                    if(f>0){var d=animation.Frames[f].RootMotion-animation.Frames[f-1].RootMotion;d.y=0;result.rootDistance+=d.magnitude;result.maxRootTravelSpeed=Mathf.Max(result.maxRootTravelSpeed,d.magnitude*Hz);}
                }
                result.meanRootTravelSpeed=result.rootDistance/Mathf.Max(result.seconds,1f/Hz);
                for(int j=0;j<bones.Length;j++){
                    result.maxLoopJointAngleDegrees=Mathf.Max(result.maxLoopJointAngleDegrees,Quaternion.Angle(data.frames[0].rotations[j],data.frames[count-1].rotations[j]));
                    result.maxLoopRootRelativePositionMetres=Mathf.Max(result.maxLoopRootRelativePositionMetres,Vector3.Distance(expected[0][j]-expected[0][0],expected[count-1][j]-expected[count-1][0]));
                }
                Capture(output,name,data,expected);
                File.WriteAllText(Path.Combine(output,name+".result.json"),JsonUtility.ToJson(result,true));
                if(result.maxForwardKinematicsErrorMetres>.002f||result.maxRoundtripRotationDegrees>.1f)throw new Exception("Conversion does not preserve pose: "+name);
                results.Add(result);UnityEngine.Object.DestroyImmediate(holder);
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        if(results.Count!=4)throw new Exception("Expected four stock clips");
        File.WriteAllText(Path.Combine(output,"passed.txt"),"4 clips converted and reconstructed within 2 mm / 0.1 degree; not integrated into game.");
        EditorApplication.Exit(0);
    }

    // Paired skeletons: source in white, reconstructed JSON in cyan. Four sampled poses.
    public static void ReviewLoop(){
        string output=Arg("-footballOutput");
        var source=JsonUtility.FromJson<Converted>(File.ReadAllText(Arg("-footballReference")));
        var adjusted=JsonUtility.FromJson<Converted>(File.ReadAllText(Arg("-footballInput")));
        var expected=new Vector3[source.frames.Length][];
        float maxAnchor=0,maxTip=0;
        int toe=Array.FindIndex(source.joints,j=>j.name=="mixamorig:LeftToeBase");
        for(int f=0;f<source.frames.Length;f++){
            expected[f]=Positions(source,f);var actual=Positions(adjusted,f);
            maxAnchor=Mathf.Max(maxAnchor,Vector3.Distance(expected[f][toe],actual[toe]));
            for(int j=0;j<source.joints.Length;j++)if(source.joints[j].parent==toe)maxTip=Mathf.Max(maxTip,Vector3.Distance(expected[f][j],actual[j]));
        }
        if(maxAnchor>.000001f)throw new Exception("Toe anchor moved");
        Capture(output,"sprint-loop",adjusted,expected,new[]{56,60,63,64});
        File.WriteAllText(Path.Combine(output,"passed.txt"),"Toe base displacement metres: "+maxAnchor+"; toe tip displacement metres: "+maxTip+". Static pose review only, no retargeting/contact validation.");
        EditorApplication.Exit(0);
    }
    static Vector3[] Positions(Converted data,int f){
        var positions=new Vector3[data.joints.Length];var rotations=new Quaternion[data.joints.Length];
        for(int j=0;j<positions.Length;j++){int p=data.joints[j].parent;rotations[j]=j==0?data.frames[f].rotations[j]:rotations[p]*data.frames[f].rotations[j];positions[j]=j==0?data.frames[f].root:positions[p]+rotations[p]*data.joints[j].offset;}
        return positions;
    }
    static void Capture(string output,string name,Converted data,Vector3[][] expected,int[] samples=null){
        var container=new GameObject("Conversion review");
        var material=new Material(Shader.Find("Hidden/Internal-Colored"));
        var cameraObject=new GameObject("Review camera");var camera=cameraObject.AddComponent<Camera>();
        var target=new RenderTexture(1600,600,24);var image=new Texture2D(1600,600,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try{
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.065f);
            camera.orthographic=true;camera.orthographicSize=2.5f;camera.transform.position=new Vector3(5.5f,1f,-15f);camera.transform.rotation=Quaternion.identity;camera.targetTexture=target;camera.cullingMask=1<<31;
            for(int sample=0;sample<4;sample++){
                int f=samples==null?sample*(data.frames.Length-1)/3:samples[sample];var positions=new Vector3[data.joints.Length];var rotations=new Quaternion[data.joints.Length];
                for(int j=0;j<data.joints.Length;j++){
                    int parent=data.joints[j].parent;
                    rotations[j]=j==0?data.frames[f].rotations[j]:rotations[parent]*data.frames[f].rotations[j];
                    positions[j]=j==0?data.frames[f].root:positions[parent]+rotations[parent]*data.joints[j].offset;
                    if(j==0)continue;
                    for(int pair=0;pair<2;pair++){
                        var lineObject=new GameObject("Bone");lineObject.transform.SetParent(container.transform);lineObject.layer=31;
                        var line=lineObject.AddComponent<LineRenderer>();line.sharedMaterial=material;line.startWidth=line.endWidth=.012f;line.positionCount=2;
                        line.startColor=line.endColor=pair==0?Color.white:Color.cyan;
                        var points=pair==0?expected[f]:positions;var shift=new Vector3(sample*3.2f+pair*1.1f,1f,0)-points[0];
                        line.SetPosition(0,points[parent]+shift);line.SetPosition(1,points[j]+shift);
                    }
                }
            }
            camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,600),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
        }finally{RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(container);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
    }
}
