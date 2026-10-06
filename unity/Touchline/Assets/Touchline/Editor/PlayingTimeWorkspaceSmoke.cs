using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;
namespace Touchline.Editor
{
 [InitializeOnLoad] public static class PlayingTimeWorkspaceSmoke
 {
  static int stage,frames,last=-1;static string output,own,external;static RenderTexture target;
  static VisualElement Root=>TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;
  static PlayingTimeWorkspaceSmoke(){EditorApplication.update+=Tick;}
  static string Output(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-touchlineOutput");return Path.GetFullPath(i>=0&&i+1<args.Length?args[i+1]:"../../artifacts/unity/playing-time-workspace-037");}
  public static void Run(){output=Output();if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Review output already exists");Directory.CreateDirectory(output);ProjectBuilder.Configure();SessionState.SetBool("PlayingTimeWorkspaceSmoke",true);EditorApplication.isPlaying=true;}
  static void Require(bool value,string message){if(!value)throw new Exception(message);}
  static void Invoke(string name,params object[] args)=>typeof(TouchlineApp).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(TouchlineApp.Instance,args);
  static void Click(string label){var b=Root.Query<Button>().ToList().First(x=>x.name==label||x.text==label);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
  static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
  static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
  static DropdownField Role=>Root.Q<DropdownField>("negotiation-role");
  static void Reveal(string name){var item=Root.Q<VisualElement>(name);item?.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(item);}
  static void Tick(){if(!SessionState.GetBool("PlayingTimeWorkspaceSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null)return;if(last==Time.frameCount)return;last=Time.frameCount;if(++frames<30)return;frames=0;try{output=Output();var app=TouchlineApp.Instance;var career=app.Career;var db=app.Database;switch(stage++){
   case 0:app.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(app.GetComponent<UIDocument>().panelSettings);career.EnsureWorld(db);Resize(1600,700);own=db.Squad(career.club).First(p=>p.age>=24).id;career.Contract(db,own).role="key";Invoke("TransferDialog",own);break;
   case 1:Require(Role.choices.SequenceEqual(PlayingTimeRoles.All.Select(PlayingTimeRoles.Label)),"Five role choices do not map to Core ids");Require(Role.value==PlayingTimeRoles.Label("key"),"Renewal did not restore current promise");foreach(string id in PlayingTimeRoles.All){Role.index=Array.IndexOf(PlayingTimeRoles.All,id);Require(Root.Q<Label>("negotiation-role-description").text.Contains(PlayingTimeRoles.Description(id)),"Description mismatch for "+id);}Require(!Root.Q<Button>("negotiation-send").enabledInHierarchy&&Root.Q<Label>("negotiation-role-description").text.Contains("moins de 24 ans"),"Older player youth promise remained actionable");Role.index=Array.IndexOf(PlayingTimeRoles.All,"impact_sub");Require(Root.Q<Button>("negotiation-send").enabledInHierarchy,"Valid role did not restore send action");Reveal("negotiation-role-description");break;
   case 2:Capture("promise-renewal-wide");Resize(1080,2520);break;
   case 3:Require(Role.value==PlayingTimeRoles.Label("impact_sub"),"Promise draft lost on resize");Capture("promise-renewal-portrait");career.Contract(db,own).role="impact_sub";app.PlayerProfile(own);Click("Contrat");break;
   case 4:Require(Root.Q<Label>("profile-playing-time-description").text==PlayingTimeRoles.Description("impact_sub"),"Profile lacks promised-role explanation");Require(Root.Q<Label>("profile-playing-time-progress")!=null,"Profile lacks actual usage report");Capture("promise-profile-portrait");Resize(1600,700);break;
   case 5:Capture("promise-profile-wide");external=db.players.First(p=>p.team!=career.club&&p.team!="free"&&p.team!="retired"&&p.age>=18&&db.clubs.Any(c=>c.id==p.team)&&career.Contract(db,p.id).parent==null&&career.Contract(db,p.id).until>=career.life.day+60).id;career.world.offers.RemoveAll(o=>o.player==external);Invoke("TransferDialog",external);break;
   case 6:Role.index=Array.IndexOf(PlayingTimeRoles.All,"impact_sub");Root.Q<Toggle>("negotiation-loan").value=true;Role.index=Array.IndexOf(PlayingTimeRoles.All,"starter");Root.Q<Toggle>("negotiation-loan").value=false;Require(Role.value==PlayingTimeRoles.Label("impact_sub"),"Permanent promise lost switching loan mode");Root.Q<Toggle>("negotiation-loan").value=true;Require(Role.value==PlayingTimeRoles.Label("starter"),"Loan promise lost switching permanent mode");Reveal("negotiation-role-description");break;
   case 7:Capture("promise-loan-wide");Invoke("Navigate","Recrutement");break;
   case 8:var role=Root.Q<DropdownField>("recruit-role");Require(role!=null,"Recruitment position filter missing");Require(role.formatSelectedValueCallback("LW")=="Ailier gauche"&&role.formatListItemCallback("RB")=="Défenseur droit","Recruitment position formatter is not French");role.value="LW";Require(role.value=="LW","Localized filter changed the simulation id");break;
   case 9:Require(Root.Q<DropdownField>("recruit-role").value=="LW","Recruitment filter did not retain its underlying position code");Capture("positions-recruitment-wide");Invoke("Navigate","Tactique");break;
   case 10:Capture("positions-tactics-wide");Require(Root.Query<Label>().ToList().Any(l=>l.text.Contains("GB")),"Tactic lacks French goalkeeper label");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"fivePromises\":true,\"renewalPreservesPromise\":true,\"youthAgeGuard\":true,\"independentLoanAndPermanentDrafts\":true,\"profileActualUsage\":true,\"positionFilterIdsPreserved\":true,\"syntheticCareerFixtures\":true,\"physicalAndroid\":false}");SessionState.SetBool("PlayingTimeWorkspaceSmoke",false);Debug.Log("TOUCHLINE_PLAYING_TIME_WORKSPACE_OK");EditorApplication.Exit(0);break;
  }}catch(Exception e){SessionState.SetBool("PlayingTimeWorkspaceSmoke",false);if(output!=null)File.WriteAllText(Path.Combine(output,"failure.txt"),"Stage "+stage+Environment.NewLine+e);Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}
