using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;

namespace Touchline.Editor
{
    // Unity can infer INTERNET from a linked module although all game data is local.
    public sealed class OfflineAndroidManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder=>1000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest=Path.Combine(path,"src","main","AndroidManifest.xml");
            var document=XDocument.Load(manifest);XNamespace android="http://schemas.android.com/apk/res/android";
            foreach(var permission in document.Root.Elements("uses-permission").Where(e=>(string)e.Attribute(android+"name")=="android.permission.INTERNET").ToArray())permission.Remove();
            document.Save(manifest);
        }
    }
}
