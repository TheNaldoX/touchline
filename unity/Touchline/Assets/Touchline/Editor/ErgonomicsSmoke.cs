using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class ErgonomicsSmoke
    {
        static int stage,frames,last=-1;static RenderTexture target;static string output;
        static readonly int[,] Sizes={{1280,966},{2160,1856},{1856,2160},{1080,2520},{1600,700},{2520,1080}};
        static readonly List<string> results=new List<string>();
        static ErgonomicsSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("ErgonomicsSmoke",true);EditorApplication.isPlaying=true;}
        static void Click(string name){var root=TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;var b=root.Query<Button>().ToList().FirstOrDefault(x=>x.text==name);if(b==null)throw new Exception("Bouton absent : "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var t=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,target.width,target.height),0,0);t.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);RenderTexture.active=old;}
        static void Check(string name)
        {
            var doc=TouchlineApp.Instance.GetComponent<UIDocument>();var root=doc.rootVisualElement;var header=root.Q(className:"header");var content=root.Q(className:"content");
            if(doc.panelSettings.scaleMode!=PanelScaleMode.ConstantPixelSize)throw new Exception("DPI scaling is still enabled");
            if(header.worldBound.height/root.worldBound.height>.24f||content.worldBound.height/root.worldBound.height<.60f)throw new Exception("Shell consumes too much screen: "+name);
            foreach(var b in root.Q(className:"navigation").Query<Button>().ToList())if(b.worldBound.yMax>root.worldBound.yMax+1||b.worldBound.xMax>root.worldBound.xMax+1||b.worldBound.width<40||b.worldBound.height<43)throw new Exception("Navigation clipped or too small: "+name);
            var next=root.Query<Button>().ToList().First(b=>b.text=="Continuer  →");if(next.worldBound.xMax>root.worldBound.xMax+1)throw new Exception("Continue clipped");
            results.Add("{\"viewport\":\""+name+"\",\"logicalWidth\":"+root.worldBound.width.ToString("0",System.Globalization.CultureInfo.InvariantCulture)+",\"contentPercent\":"+(content.worldBound.height/root.worldBound.height*100).ToString("0",System.Globalization.CultureInfo.InvariantCulture)+"}");
        }
        static void Tick()
        {
            if(!SessionState.GetBool("ErgonomicsSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<25)return;frames=0;
            try{output=Path.GetFullPath("../../artifacts/unity/ergonomics-0.13.1");Directory.CreateDirectory(output);
                if(stage==0){TouchlineApp.Instance.Career.EnsureWorld(TouchlineApp.Instance.Database);PlayerPrefs.SetInt("interface-size",1);}
                if(stage<12){int i=stage/2;if(stage%2==0){Resize(Sizes[i,0],Sizes[i,1]);Click("Bureau");}else{string name=Sizes[i,0]+"x"+Sizes[i,1];Check(name);Capture(name);}stage++;return;}
                switch(stage++){
                    case 12:Resize(1280,966);Click("Tactique");break;
                    case 13:var root=TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;if(root.Q(className:"pitch").worldBound.yMin-root.Q(className:"tactics-workspace").worldBound.yMin>20)throw new Exception("Terrain décalé vers le bas");Capture("tactique-fold");Click("Plus");break;
                    case 14:Capture("menu");Click("Réglages");break;
                    case 15:Capture("reglages");PlayerPrefs.SetInt("interface-size",2);break;
                    case 16:Capture("grande");Click("Bureau");break;
                    case 17:Check("fold-large");PlayerPrefs.SetInt("interface-size",0);break;
                    case 18:Check("fold-compact");Capture("compacte");PlayerPrefs.SetInt("interface-size",1);File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"physicalPhone\":false,\"screens\":["+string.Join(",",results)+"]}");SessionState.SetBool("ErgonomicsSmoke",false);Debug.Log("TOUCHLINE_ERGONOMICS_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("ErgonomicsSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
