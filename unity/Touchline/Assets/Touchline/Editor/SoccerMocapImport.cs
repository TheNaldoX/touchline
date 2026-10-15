using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Touchline.Editor
{
    // Source FBXs stay in an ignored Editor-only directory. Only animation curves
    // are exported: no duplicate player meshes, materials or UE5 assets in Android.
    public sealed class SoccerMocapImport : AssetPostprocessor
    {
        public const string Source="Assets/Touchline/Editor/PrivateSoccerMocap/";
        public const string Destination="Assets/Touchline/Resources/Animations/RohrSoccer/";
        bool Applies=>assetPath.Replace('\\','/').StartsWith(Source,StringComparison.Ordinal)&&assetPath.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase);
        public override uint GetVersion()=>1;
        void OnPreprocessModel()
        {
            if(!Applies)return;
            var importer=(ModelImporter)assetImporter;
            importer.animationType=ModelImporterAnimationType.Human;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.importCameras=false;importer.importLights=false;importer.importBlendShapes=false;
            importer.animationCompression=ModelImporterAnimationCompression.KeyframeReduction;
            importer.resampleCurves=true;
        }
        void OnPreprocessAnimation()
        {
            if(!Applies)return;
            var importer=(ModelImporter)assetImporter;
            var clips=importer.defaultClipAnimations;
            foreach(var clip in clips){
                clip.name="Rohr_"+Path.GetFileNameWithoutExtension(assetPath);
                // These are recorded sequences, not seamless locomotion loops.
                clip.loopTime=false;clip.loopPose=false;
                clip.lockRootRotation=true;clip.keepOriginalOrientation=false;
                clip.lockRootHeightY=true;clip.keepOriginalPositionY=true;
                clip.lockRootPositionXZ=false;clip.keepOriginalPositionXZ=false;
            }
            importer.clipAnimations=clips;
        }
        [MenuItem("Touchline/Animations/Import personal soccer mocap")]
        public static void Export()
        {
            var paths=Directory.GetFiles(Source,"*.fbx").OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            if(paths.Length!=21)throw new InvalidOperationException("Expected the 21 Rokoko_Default FBXs from Soccer pack 05.");
            Directory.CreateDirectory(Destination);AssetDatabase.Refresh();
            foreach(var path in paths){
                var assets=AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\','/'));
                var avatar=assets.OfType<Avatar>().SingleOrDefault();
                if(avatar==null||!avatar.isHuman||!avatar.isValid)throw new InvalidOperationException("Invalid Humanoid avatar: "+path);
                var clips=assets.OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
                if(clips.Length!=1||!clips[0].humanMotion||clips[0].length<=0)throw new InvalidOperationException("Invalid captured clip: "+path);
                var clip=clips[0];var target=Destination+clip.name+".anim";
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(target);
                if(existing!=null)EditorUtility.CopySerialized(clip,existing);
                else AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(clip),target);
            }
            AssetDatabase.SaveAssets();Debug.Log("TOUCHLINE_SOCCER_MOCAP_IMPORTED 21 Humanoid clips");
        }
        public static void Batch()
        {
            try{Export();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void ReportImportLogs()
        {
            try{
                var report=new System.Text.StringBuilder();
                foreach(var path in Directory.GetFiles(Source,"*.fbx").OrderBy(p=>p,StringComparer.Ordinal)){
                    var log=AssetImporter.GetImportLog(path.Replace('\\','/'));
                    report.AppendLine(Path.GetFileName(path));
                    if(log!=null)foreach(var entry in log.logEntries)report.AppendLine(entry.message);
                }
                Directory.CreateDirectory("build/film");File.WriteAllText("build/film/soccer-import-messages.txt",report.ToString());
                EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
