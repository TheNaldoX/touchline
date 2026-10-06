using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class ProfessionalSmoke
    {
        static int stage,frames,last=-1;static RenderTexture target;static string output;
        static ProfessionalSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("ProfessionalSmoke",true);EditorApplication.isPlaying=true;}
        static void Click(string text){var root=TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;var b=root.Query<Button>().ToList().FirstOrDefault(x=>x.text==text);if(b==null){var more=root.Query<Button>().ToList().FirstOrDefault(x=>x.text=="Plus");if(more!=null){using(var e=NavigationSubmitEvent.GetPooled()){e.target=more;more.SendEvent(e);}b=root.Query<Button>().ToList().FirstOrDefault(x=>x.text==text);}}if(b==null)throw new Exception("Bouton manquant : "+text);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();if(texture.GetPixels().Count(c=>c.r+c.g+c.b>.1f)<target.width*target.height/20)throw new Exception("Capture vide");File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=old;}
        static void Tick(){if(!SessionState.GetBool("ProfessionalSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null)return;if(last==Time.frameCount)return;last=Time.frameCount;if(++frames<20)return;frames=0;
            try{var app=TouchlineApp.Instance;var root=app.GetComponent<UIDocument>().rootVisualElement;output=Path.GetFullPath("../../artifacts/unity/professional");Directory.CreateDirectory(output);
                switch(stage++){
                    case 0:app.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(app.GetComponent<UIDocument>().panelSettings);app.Career.EnsureWorld(app.Database);Resize(1280,900);Click("Bureau");break;
                    case 1:Capture("bureau");Click("Calendrier");break;
                    case 2:Capture("calendrier");if(!root.Query<Label>().ToList().Any(l=>l.text.Contains("2026")))throw new Exception("Calendrier absent");Click("Recrutement");break;
                    case 3:Capture("recrutement");Click("Agent / offre");break;
                    case 4:Capture("negociation");Click("Fermer");Click("Formation");break;
                    case 5:Capture("formation");if(app.Career.world.youth.Count<14)throw new Exception("Promotion incomplète");Click("Finances");break;
                    case 6:Capture("finances");Click("Presse");break;
                    case 7:Capture("presse");Click("Carrière");break;
                    case 8:Capture("carriere");Click("Réglages");break;
                    case 9:Capture("reglages");Resize(1600,700);Click("Bureau");break;
                    case 10:Capture("paysage");CheckNavigation();Resize(900,1600);break;
                    case 11:Capture("portrait");CheckNavigation();Click("Match");break;
                    case 12:Capture("avant-match");app.Career.life.day=app.Career.life.nextFixture;Click("Déléguer à l’adjoint");break;
                    case 13:if(app.Career.life.matches!=1)throw new Exception("Match de championnat non enregistré");Click("Continuer  →");break;
                    case 14:Capture("lendemain");Click("Staff et délégation");break;
                    case 15:Capture("staff");Click("Tactique");break;
                    case 16:Capture("tactique");if(app.Career.life.day!=68)throw new Exception("Avancement incorrect");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"screens\":16,\"leagueMatchSettled\":true,\"dailyContinue\":true,\"portraitLandscapeNavigation\":true,\"physicalPhone\":false}");SessionState.SetBool("ProfessionalSmoke",false);Debug.Log("TOUCHLINE_PROFESSIONAL_SMOKE_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("ProfessionalSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void CheckNavigation(){var root=TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;foreach(var b in root.Q(className:"navigation").Query<Button>().ToList())if(b.worldBound.yMax>root.worldBound.yMax+1||b.worldBound.xMax>root.worldBound.xMax+1||b.worldBound.width<40)throw new Exception("Navigation hors écran");}
    }
}
