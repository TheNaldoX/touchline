using System;
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace Touchline.Editor
{
    // Opt-in recovery for a locked generated Gradle output. Keep the old
    // directory untouched; do not change Windows ACLs or delete caches.
    public sealed class GradleOutputRecovery : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-touchlineFreshGradleOutput")<0)return;
            var name="build-touchline-"+Guid.NewGuid().ToString("N").Substring(0,8);
            Configure(path,name,false);
            Configure(Path.Combine(Path.GetDirectoryName(path),"launcher"),name,true);
            Debug.Log("TOUCHLINE_FRESH_GRADLE_OUTPUT "+Path.Combine(path,name));
        }
        static void Configure(string path,string name,bool launcher)
        {
            const string marker="\n// Touchline: fresh generated output";
            var file=Path.Combine(path,"build.gradle");var source=File.ReadAllText(file);int old=source.IndexOf(marker,StringComparison.Ordinal);if(old>=0)source=source.Substring(0,old);
            source+=marker+" after a Windows cache lock.\nlayout.buildDirectory.set(layout.projectDirectory.dir('"+name+"'))\n";
            // Unity retrieves the APK from the standard launcher output path.
            // Only copy the finished artifact there; intermediates stay fresh.
            if(launcher)source+="tasks.register('touchlinePublishApk', Copy) {\n    from(layout.buildDirectory.dir('outputs/apk/release'))\n    into(layout.projectDirectory.dir('build/outputs/apk/release'))\n}\nafterEvaluate { tasks.named('assembleRelease').configure { finalizedBy('touchlinePublishApk') } }\n";
            File.WriteAllText(file,source);
        }
    }
}
