using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Touchline.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Touchline.Editor {
 [InitializeOnLoad] public static class NavigationPersistenceSmoke {
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static int stage,frames,last=-1,baseline;static Career previousCareer;static bool revealBefore;static string lazyPlayer;
  static NavigationPersistenceSmoke(){EditorApplication.update+=Tick;}
  public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("NavigationPersistenceSmoke",true);EditorApplication.isPlaying=true;}
  static TouchlineApp App=>TouchlineApp.Instance;
  static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
  static object Call(string name,params object[] args)=>typeof(TouchlineApp).GetMethod(name,Private).Invoke(App,args);
  static bool Pending=>(bool)typeof(TouchlineApp).GetField("queuedPreferenceSave",Private).GetValue(App);
  static void Check(bool condition,string error){if(!condition)throw new Exception(error+"; saveRequests="+App.SaveRequestCount+"; queued="+Pending);}
  static void Navigate(string destination)=>Call("Navigate",destination);
  static void Click(string name){var b=Root.Query<Button>().ToList().FirstOrDefault(x=>x.name==name||x.text==name);Check(b!=null,"Missing button "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
  static void Tick(){if(!SessionState.GetBool("NavigationPersistenceSmoke",false)||!EditorApplication.isPlaying||App==null)return;if(last==Time.frameCount)return;last=Time.frameCount;if(++frames<20)return;frames=0;
   try{switch(stage++){
    case 0:
     Check((bool)typeof(TouchlineApp).GetProperty("VisualValidation",Private).GetValue(App),"Smoke must never write personal save files");
     Check(App.SaveRequestCount==1,"New career initialization must request exactly one startup save");
     App.Career.EnsureWorld(App.Database);
     // Normalize lazy legacy initializations once before testing unchanged navigation.
     foreach(var menu in new[]{"Club","Effectif","Calendrier","Recrutement","Staff et délégation","Finances","Formation","Messages","Santé","Infrastructures","Carrière","Tactique","Réglages"})Navigate(menu);
     App.Save();baseline=App.SaveRequestCount;break;
    case 1:
     foreach(var menu in new[]{"Club","Effectif","Calendrier","Recrutement","Staff et délégation","Finances","Formation","Santé","Infrastructures","Carrière","Tactique","Réglages","Messages","Club"})Navigate(menu);
     Check(App.SaveRequestCount==baseline,"Unchanged menus requested serialization");Check(!Pending,"Unchanged menus queued a deferred save");Call("HistoryStep",false);Call("HistoryStep",true);Check(App.SaveRequestCount==baseline&&!Pending,"History-only navigation requested serialization");
     Call("QueuePreferenceSave");Navigate("Effectif");Check(App.SaveRequestCount==baseline+1&&!Pending,"Navigation did not flush a pending preference exactly once");baseline=App.SaveRequestCount;Navigate("Club");Check(App.SaveRequestCount==baseline,"Flushed preference kept saving");break;
    case 2:
     App.Career.life.facilities.Clear();Navigate("Infrastructures");Check(App.Career.life.facilities.Count>=4,"Legacy facility initialization failed");Check(App.SaveRequestCount==baseline+1&&!Pending,"Legacy facility repair was not saved exactly once");baseline=App.SaveRequestCount;Navigate("Infrastructures");Check(App.SaveRequestCount==baseline,"Repaired facilities save repeatedly");
     App.Career.life.staff.marketBound=false;App.Career.life.staff.members.Clear();Navigate("Staff et délégation");Check(App.Career.life.staff.marketBound&&App.Career.life.staff.members.Count>0,"Legacy staff binding failed");Check(App.SaveRequestCount==baseline+1&&!Pending,"Legacy staff repair was not saved exactly once");baseline=App.SaveRequestCount;Navigate("Staff et délégation");Check(App.SaveRequestCount==baseline,"Bound staff saves repeatedly");break;
    case 3:
     lazyPlayer=App.Database.players.First(p=>p.team!=App.Career.club&&!App.Career.world.contracts.Any(c=>c.player==p.id)).id;
     int contracts=App.Career.world.contracts.Count;var employment=App.Career.Contract(App.Database,lazyPlayer);
     Check(App.Career.world.contracts.Count==contracts+1&&Pending,"Lazy displayed contract was not marked for persistence");
     Navigate("Club");Check(App.SaveRequestCount==baseline+1&&!Pending,"Lazy displayed contract did not flush once");baseline=App.SaveRequestCount;
     Check(ReferenceEquals(employment,App.Career.Contract(App.Database,lazyPlayer)),"Existing contract retrieval replaced employment");Navigate("Effectif");Check(App.SaveRequestCount==baseline&&!Pending,"Reading an existing contract saved again");break;
    case 4:
     revealBefore=App.Career.revealAttributes;Navigate("Changer de club");Root.Q<Toggle>("new-career-reveal-attributes").value=!revealBefore;
     Check(App.Career.revealAttributes==revealBefore&&!Pending,"New career option mutated the current career");Navigate("Club");Navigate("Changer de club");
     Check(Root.Q<Toggle>("new-career-reveal-attributes").value==!revealBefore,"New career draft was lost when changing menus");Check(App.SaveRequestCount==baseline,"Draft-only navigation saved the current career");Navigate("Club");break;
    case 5:
     string opponent=App.Database.clubs.First(c=>c.playable&&c.id!=App.Career.club&&c.league==App.Database.clubs.First(t=>t.id==App.Career.club).league).id;
     App.Career.PrepareLineup(App.Database);var sim=MatchSimulation.Create(App.Database,App.Career,opponent,987,2700);App.Career.match=sim.State;App.Career.ApplyMatchContext(sim);
     Call("CreateArena",sim);var arena=(MatchArena)typeof(TouchlineApp).GetField("arena",Private).GetValue(App);arena.Paused=true;float seconds=sim.State.clock;
     baseline=App.SaveRequestCount;Navigate("Tactique");Check(App.SaveRequestCount==baseline+1&&ReferenceEquals(App.Career.match,sim.State)&&arena.Paused&&sim.State.clock==seconds,"Navigation failed to persist and pause an ongoing match");
     baseline=App.SaveRequestCount;Navigate("Club");Check(App.SaveRequestCount==baseline+1,"An ongoing match must still flush when navigating");
     // This lifecycle test intentionally completes the valid roster fixture, not the simulation balance.
     sim.State.finished=true;App.Career.life.recordedMatch=false;var lineup=sim.State.actors.Where(p=>p.side==0).OrderBy(p=>p.slot).Select(p=>p.id).ToArray();baseline=App.SaveRequestCount;Navigate("Club");
     Check(App.Career.match==null&&App.Career.life.recordedMatch&&App.Career.lineup.SequenceEqual(lineup),"Finished match navigation failed to record result and lineup");Check(App.SaveRequestCount==baseline+1,"Clearing a finished match lost its persistence flush");
     baseline=App.SaveRequestCount;Navigate("Effectif");Check(App.SaveRequestCount==baseline,"Finished result saves on every subsequent menu");break;
    case 6:
     previousCareer=App.Career;Navigate("Changer de club");Click("Choisir");Click("Confirmer");
     Check(!ReferenceEquals(previousCareer,App.Career)&&App.Career.revealAttributes==!revealBefore,"Confirmed new career did not consume the scouting option draft");
     App.Save();baseline=App.SaveRequestCount;
     string missing=App.Database.players.First(p=>p.team!=previousCareer.club&&!previousCareer.world.contracts.Any(c=>c.player==p.id)).id;previousCareer.Contract(App.Database,missing);
     Check(!Pending,"An abandoned career still queues saves in the new career");Navigate("Club");Check(App.SaveRequestCount==baseline,"New career neutral menu saved unnecessarily");break;
    case 7:
     var output=Path.GetFullPath("../../artifacts/unity/navigation-persistence");Directory.CreateDirectory(output);
     File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"saveRequests\":"+App.SaveRequestCount+",\"personalSaveWrites\":false,\"unchangedMenus\":true,\"pendingPreference\":true,\"facilityAndStaffMigration\":true,\"lazyContract\":true,\"newCareerDraft\":true,\"ongoingAndFinishedMatchFlush\":true,\"oldCareerUnsubscribed\":true,\"physicalAndroid\":false}");
     SessionState.SetBool("NavigationPersistenceSmoke",false);Debug.Log("TOUCHLINE_NAVIGATION_PERSISTENCE_OK");EditorApplication.Exit(0);break;
   }}catch(Exception e){SessionState.SetBool("NavigationPersistenceSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}

