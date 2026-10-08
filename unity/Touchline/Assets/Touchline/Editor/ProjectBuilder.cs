using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

namespace Touchline.Editor
{
    public static class ProjectBuilder
    {
        const string Root="Assets/Touchline";
        [MenuItem("Touchline/PrÃ©parer le projet Android")]
        public static void Configure()
        {
            Directory.CreateDirectory(Root+"/Resources/Rendering");Directory.CreateDirectory(Root+"/Scenes");AssetDatabase.Refresh();
            ConfigureBranding();
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Root+"/Resources/Logos"})){
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if(PlayerTextureImport.ConfigureBadge(importer))importer.SaveAndReimport();
            }
            foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{Root+"/Resources/Audio"})){
                var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));var sample=importer.defaultSampleSettings;
                if(sample.loadType!=AudioClipLoadType.Streaming){sample.loadType=AudioClipLoadType.Streaming;sample.compressionFormat=AudioCompressionFormat.Vorbis;sample.quality=.7f;importer.defaultSampleSettings=sample;importer.loadInBackground=true;importer.SaveAndReimport();}
            }
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root+"/Resources/Rendering/MatchRenderer.asset");
            if(renderer==null){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,Root+"/Resources/Rendering/MatchRenderer.asset");}
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Resources/Rendering/TouchlineURP.asset");
            if(pipeline==null){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,Root+"/Resources/Rendering/TouchlineURP.asset");}
            pipeline.renderScale=.9f;pipeline.msaaSampleCount=2;pipeline.shadowDistance=55;pipeline.mainLightShadowmapResolution=2048;pipeline.shadowCascadeCount=2;pipeline.cascade2Split=.35f;pipeline.shadowDepthBias=.3f;pipeline.shadowNormalBias=.35f;var shadowSettings=new SerializedObject(pipeline);shadowSettings.FindProperty("m_SoftShadowsSupported").boolValue=true;shadowSettings.ApplyModifiedPropertiesWithoutUndo();pipeline.supportsCameraDepthTexture=false;pipeline.supportsCameraOpaqueTexture=false;
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;QualitySettings.shadows=UnityEngine.ShadowQuality.All;
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("Shader URP Lit absent.");if(!File.Exists(Root+"/Resources/Rendering/PlayerMaterial.mat"))AssetDatabase.CreateAsset(new Material(shader),Root+"/Resources/Rendering/PlayerMaterial.mat");
            var hair=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Resources/Rendering/HairMaterial.mat");
            if(hair==null){hair=new Material(shader);AssetDatabase.CreateAsset(hair,Root+"/Resources/Rendering/HairMaterial.mat");}
            hair.SetFloat("_AlphaClip",1);hair.SetFloat("_Cutoff",.42f);hair.SetFloat("_Cull",0);hair.EnableKeyword("_ALPHATEST_ON");hair.SetOverrideTag("RenderType","TransparentCutout");hair.renderQueue=2450;EditorUtility.SetDirty(hair);
            var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(Root+"/Resources/TouchlinePanel.asset");if(panel==null){panel=ScriptableObject.CreateInstance<PanelSettings>();AssetDatabase.CreateAsset(panel,Root+"/Resources/TouchlinePanel.asset");}
            panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(1280,900);panel.screenMatchMode=PanelScreenMatchMode.MatchWidthOrHeight;panel.match=.5f;panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root+"/Resources/UI/RuntimeTheme.tss");
            panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;EditorUtility.SetDirty(panel);
            PlayerSettings.companyName="Touchline Personal";PlayerSettings.productName="Touchline Unity";PlayerSettings.bundleVersion="0.58.0-preview.1";PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"fr.personal.touchline.unity");
            PlayerSettings.Android.bundleVersionCode=51;PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;PlayerSettings.allowedAutorotateToPortrait=true;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;PlayerSettings.allowedAutorotateToLandscapeLeft=true;PlayerSettings.allowedAutorotateToLandscapeRight=true;
            PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.runInBackground=false;PlayerSettings.Android.forceInternetPermission=false;PlayerSettings.Android.forceSDCardPermission=false;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var app=new GameObject("Touchline Native Application");app.AddComponent<UIDocument>().panelSettings=panel;app.AddComponent<TouchlineApp>();EditorSceneManager.SaveScene(scene,Root+"/Scenes/Touchline.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/Touchline.unity",true)};
            AssetDatabase.SaveAssets();Debug.Log("TOUCHLINE_CONFIGURATION_OK");
        }
        public static void Android()
        {
            Configure();UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath=Path.Combine(EditorApplication.applicationContentsPath,"PlaybackEngines/AndroidPlayer/OpenJDK");var output=Path.GetFullPath("../../artifacts/Touchline-Unity-0.58-preview.apk");if(File.Exists(output))throw new IOException("APK already exists; choose a new version.");Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Root+"/Scenes/Touchline.unity"},locationPathName=output,target=BuildTarget.Android,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Ã‰chec du build Android : "+report.summary.result);Debug.Log("TOUCHLINE_ANDROID_OK "+output);
        }
        // Build Android lancé par GitHub Actions (game-ci) : le JDK et le SDK viennent de l'image Docker,
        // la sortie va dans build/Android/ (dossier récupéré par le workflow).
        public static void AndroidCI()
        {
            Configure();ApplyCiKeystore();var output=Path.GetFullPath("build/Android/Touchline-Unity-0.58-preview.apk");Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Root+"/Scenes/Touchline.unity"},locationPathName=output,target=BuildTarget.Android,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded){Debug.LogError("Echec du build Android : "+report.summary.result);EditorApplication.Exit(1);}
            Debug.Log("TOUCHLINE_ANDROID_OK "+output);
        }
        // game-ci décode le secret ANDROID_KEYSTORE_BASE64 dans le projet et transmet nom, mot de passe et alias
        // en arguments (-androidKeystoreName…). Sans eux, Unity signe avec une clé de debug jetable.
        static void ApplyCiKeystore()
        {
            string Arg(string name){var a=Environment.GetCommandLineArgs();for(int i=0;i<a.Length-1;i++)if(a[i]==name)return a[i+1];return null;}
            var store=Arg("-androidKeystoreName");var pass=Arg("-androidKeystorePass");
            if(string.IsNullOrEmpty(store)||string.IsNullOrEmpty(pass)||!File.Exists(store)){PlayerSettings.Android.useCustomKeystore=false;Debug.LogWarning("TOUCHLINE_KEYSTORE absent : signature de debug.");return;}
            PlayerSettings.Android.useCustomKeystore=true;PlayerSettings.Android.keystoreName=Path.GetFullPath(store);PlayerSettings.Android.keystorePass=pass;
            PlayerSettings.Android.keyaliasName=Arg("-androidKeyaliasName")??"touchline";PlayerSettings.Android.keyaliasPass=Arg("-androidKeyaliasPass")??pass;
        }
        static void ConfigureBranding()
        {
            foreach(var path in new[]{Root+"/Resources/UI/Branding/touchline-logo-v1.png",Root+"/Branding/touchline-icon-v1.png"}){
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)throw new FileNotFoundException("Identité Touchline absente",path);
                if(importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed||importer.npotScale!=TextureImporterNPOTScale.None||importer.maxTextureSize!=2048){
                    importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.SaveAndReimport();
                }
            }
            var icon=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Branding/touchline-icon-v1.png");
            var target=UnityEditor.Build.NamedBuildTarget.Unknown;var sizes=PlayerSettings.GetIconSizes(target,IconKind.Any);
            var icons=new Texture2D[Math.Max(1,sizes.Length)];for(int i=0;i<icons.Length;i++)icons[i]=icon;
            PlayerSettings.SetIcons(target,icons,IconKind.Any);
        }
    }
}




