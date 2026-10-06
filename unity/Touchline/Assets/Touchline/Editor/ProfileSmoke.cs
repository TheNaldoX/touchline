using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class ProfileSmoke
    {
        static int stage,frames,last=-1,loaded;static string tiredPlayer,selectedSquadPlayer;static float originalFitness;static float deadline;static string own,foreign,missing,output;static RenderTexture target;static PortraitStore store;
        static VisualElement Root=>TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;
        static ProfileSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("ProfileSmoke",true);EditorApplication.isPlaying=true;}
        static void Click(string text){var b=Root.Query<Button>().ToList().FirstOrDefault(x=>x.text==text);if(b==null)throw new Exception("Missing "+text);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;}
        static void CheckBounds(){var panel=Root.Q("player-profile");if(panel.worldBound.xMin<0||panel.worldBound.xMax>Root.worldBound.xMax+1||panel.worldBound.yMax>Root.worldBound.yMax+1)throw new Exception("Profile overflows");foreach(var b in panel.Q(className:"profile-tabs").Query<Button>().ToList())if(b.worldBound.width<50||b.worldBound.height<43||b.worldBound.xMax>panel.worldBound.xMax)throw new Exception("Profile tab inaccessible");}
        static bool Displayed(VisualElement element){for(var p=element;p!=null;p=p.parent)if(p.resolvedStyle.display==DisplayStyle.None)return false;return true;}
        static void CheckSquad(){var list=Root.Q<ListView>("squad-list");if(list==null||list.worldBound.height<100)throw new Exception("Squad list has no usable height");var rows=Root.Query<VisualElement>(className:"squad-player-row").ToList().Where(Displayed).ToList();if(rows.Count>=list.itemsSource.Count&&list.itemsSource.Count>20)throw new Exception("Squad rows are not virtualized");foreach(var row in rows){var photo=row.Q<Image>();string id=row.name.Substring("squad-row-".Length);if(photo.image!=null&&photo.image.name!="Portrait "+id)throw new Exception("Recycled row shows another player portrait");var button=row.Q<Button>();if(button.worldBound.width<43||button.worldBound.height<43||button.worldBound.xMax>Root.worldBound.xMax+1)throw new Exception("Squad profile button inaccessible: "+button.name+" "+button.worldBound+" root "+Root.worldBound);}if(TouchlineApp.Instance.GetComponents<PortraitStore>().Any(p=>p.CachedCount>24))throw new Exception("Squad portrait cache unbounded");}
        static void Tick(){if(!SessionState.GetBool("ProfileSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<28)return;frames=0;
            try{var app=TouchlineApp.Instance;output=Path.GetFullPath("../../artifacts/unity/profiles-0.16");Directory.CreateDirectory(output);
                switch(stage++){
                    case 0:app.Career.EnsureWorld(app.Database);app.Career.revealAttributes=false;store=app.gameObject.AddComponent<PortraitStore>();own=app.Database.Squad(app.Career.club).First(p=>store.Contains(p.id)).id;foreign=app.Database.players.First(p=>p.team!=app.Career.club&&app.Career.Knowledge(p.id)==0&&p.attributes?.Length>10).id;missing=app.Database.players.First(p=>!store.Contains(p.id)).id;Resize(1280,966);app.PlayerProfile(own);deadline=Time.realtimeSinceStartup+15;break;
                    case 1:if(Root.Q<Image>("player-photo")==null){if(Time.realtimeSinceStartup>deadline)throw new Exception("Local portrait failed to load");stage--;return;}CheckBounds();Capture("overview");Click("Attributs");break;
                    case 2:if(!Root.Query<Label>(className:"attribute-score").ToList().All(l=>int.TryParse(l.text,out int value)&&value>=1&&value<=20))throw new Exception("Own attributes incorrect");if(Root.Query<Label>().ToList().Any(l=>l.text=="finishing"||l.text=="sprintSpeed"))throw new Exception("Untranslated attribute");Capture("attributes");Click("Contrat");break;
                    case 3:if(!Root.Query<Label>().ToList().Any(l=>l.text=="Salaire mensuel"))throw new Exception("Monthly salary absent");Capture("contract");Click("Relations");break;
                    case 4:Capture("relations");app.PlayerProfile(foreign);Click("Attributs");break;
                    case 5:if(Root.Query<Label>(className:"attribute-score").ToList().Any(l=>l.text!="—")||Root.Query<Label>(className:"attribute-strong").ToList().Any())throw new Exception("Scouting secrecy leaked");Capture("unknown");app.Career.world.reports.Add(new ScoutReport{player=foreign,confidence=50,estimate=app.Database.Find(foreign).rating-2});app.PlayerProfile(foreign);Click("Attributs");break;
                    case 6:if(Root.Query<Label>(className:"attribute-score").ToList().Any(l=>!l.text.Contains("–")))throw new Exception("Partial scouting must show ranges");Capture("partial");Click("Fermer");Resize(1080,2520);break;
                    case 7:app.PlayerProfile(own);Click("Attributs");break;
                    case 8:CheckBounds();Capture("portrait");app.PlayerProfile(missing);break;
                    case 9:if(Root.Q(className:"portrait-missing")==null||Root.Q("player-photo")!=null)throw new Exception("Missing portrait should use neutral fallback");Capture("fallback");Click("Fermer");Resize(1600,700);break;
                    case 10:app.PlayerProfile(own);Click("Attributs");break;
                    case 11:CheckBounds();Capture("landscape");Click("Fermer");var ids=app.Database.players.Where(p=>store.Contains(p.id)).Take(30).Select(p=>p.id);foreach(var id in ids)store.Load(id,t=>{if(t!=null)loaded++;});deadline=Time.realtimeSinceStartup+20;break;
                    case 12:if(loaded<30){if(Time.realtimeSinceStartup>deadline)throw new Exception("Portrait cache loads timed out");stage--;return;}if(store.CachedCount>24)throw new Exception("Unbounded portrait cache");Resize(1080,2520);app.PlayerProfile(app.Database.players.First(p=>p.heightCm>0&&p.weightKg>0).id);break;
                    case 13:Root.Q<ScrollView>("profile-body").ScrollTo(Root.Q("profile-physique"));break;
                    case 14:CheckBounds();var physique=Root.Q("profile-physique");if(!physique.Query<Label>().ToList().Any(l=>l.text.EndsWith(" m"))||!physique.Query<Label>().ToList().Any(l=>l.text.EndsWith(" kg")))throw new Exception("Imported physique not shown");Capture("physique");app.PlayerProfile(app.Database.players.First(p=>p.heightCm==0).id);break;
                    case 15:Root.Q<ScrollView>("profile-body").ScrollTo(Root.Q("profile-physique"));break;
                    case 16:if(!Root.Q("profile-physique").Query<Label>().ToList().Any(l=>l.text=="Non renseignée"))throw new Exception("Unknown height invented in profile");Capture("physique-unknown");Click("Fermer");Click("Effectif");break;
                    case 17:CheckSquad();if(!Root.Query<VisualElement>(className:"squad-player-row").ToList().Any(r=>r.Q<Image>().image!=null))throw new Exception("Squad portraits missing");Capture("squad-portrait");var first=Root.Query<Button>(className:"squad-open").ToList().First();selectedSquadPlayer=first.name.Substring("squad-open-".Length);using(var open=NavigationSubmitEvent.GetPooled()){open.target=first;first.SendEvent(open);}break;
                    case 18:if(!Root.Q("player-profile").Query<Label>().ToList().Any(l=>l.text==app.Database.Find(selectedSquadPlayer).name))throw new Exception("Squad opened wrong player");Click("Fermer");Root.Q<TextField>("squad-search").value="NO_SUCH_PLAYER_173";break;
                    case 19:if(Root.Q<ListView>("squad-list").itemsSource.Count!=0||Root.Q("squad-empty").resolvedStyle.display==DisplayStyle.None)throw new Exception("Squad empty search broken");Root.Q<TextField>("squad-search").value="";var tired=app.Database.Squad(app.Career.club).First(p=>app.Career.Available(p.id));tiredPlayer=tired.id;originalFitness=tired.fitness;tired.fitness=62;Root.Q<DropdownField>("squad-group").value="À ménager";break;
                    case 20:var filtered=Root.Q<ListView>("squad-list");if(filtered.itemsSource.Count!=1||((PlayerData)filtered.itemsSource[0]).id!=tiredPlayer)throw new Exception("Fitness filter ignores real condition");Capture("squad-tired");app.Database.Find(tiredPlayer).fitness=originalFitness;Root.Q<DropdownField>("squad-group").value="Tous";Root.Q<DropdownField>("squad-position").value="GB";break;
                    case 21:if(Root.Q<ListView>("squad-list").itemsSource.Cast<PlayerData>().Any(p=>p.position!="GB"))throw new Exception("Squad position filter broken");Root.Q<DropdownField>("squad-position").value="Tous postes";Root.Q<ListView>("squad-list").ScrollToItem(-1);break;
                    case 22:CheckSquad();Capture("squad-bottom");Resize(1280,966);break;
                    case 23:CheckSquad();Root.Q<ListView>("squad-list").ScrollToItem(0);break;
                    case 24:CheckSquad();Capture("squad-open");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"bundledPortrait\":true,\"fallback\":true,\"attributesInFrench\":true,\"scoutingHidden\":true,\"scoutingRanges\":true,\"monthlyContract\":true,\"portraitLandscape\":true,\"boundedCache\":true,\"physicalAndroid\":false}");SessionState.SetBool("ProfileSmoke",false);Debug.Log("TOUCHLINE_PROFILE_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("ProfileSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
