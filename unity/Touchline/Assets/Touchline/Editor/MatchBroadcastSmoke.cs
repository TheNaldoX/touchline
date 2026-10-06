using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class MatchBroadcastSmoke
    {
        static int stage,frames,last=-1,preference;static RenderTexture target;static string output;static MatchArena arena;static float pausedClock;static string selected;
        static VisualElement Root=>TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;
        static MatchBroadcastSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){SessionState.SetInt("BroadcastPreviousView",PlayerPrefs.GetInt("match-view-mode",-1));preference=PlayerPrefs.GetInt("match-highlights",1);SessionState.SetInt("BroadcastPreviousMode",preference);SessionState.SetInt("BroadcastPreviousCamera",PlayerPrefs.GetInt("match-camera-mode",0));SessionState.SetInt("BroadcastPreviousSpeed",PlayerPrefs.GetInt("match-live-speed",1));SessionState.SetFloat("BroadcastPreviousZoom",PlayerPrefs.GetFloat("match-camera-zoom",1));PlayerPrefs.SetInt("match-camera-mode",0);PlayerPrefs.SetInt("match-live-speed",1);PlayerPrefs.SetFloat("match-camera-zoom",1);ProjectBuilder.Configure();SessionState.SetBool("MatchBroadcastSmoke",true);EditorApplication.isPlaying=true;}
        static void Click(string name){var button=Root.Query<Button>().ToList().FirstOrDefault(b=>b.name==name||b.text==name);if(button==null)throw new Exception("Missing button "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
        static void Resize(int width,int height){if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}target=new RenderTexture(width,height,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;}
        static void Capture(string name)
        {
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
        }
        static void CheckDashboard()
        {
            var dashboard=Root.Q("match-interlude");if(dashboard.resolvedStyle.display==DisplayStyle.None||!arena.QuietPresentation||arena.MatchCamera.enabled)throw new Exception("Calm dashboard/camera state incorrect");
            var left=Root.Q("interlude-team-0");var right=Root.Q("interlude-team-1");if(left.worldBound.xMax>right.worldBound.xMin+2||right.worldBound.xMax>Root.worldBound.xMax+1||left.worldBound.width<120)throw new Exception("Lineups overlap or overflow");
            for(int i=0;i<22;i++){var button=Root.Q<Button>("interlude-player-"+i);if(button==null||button.worldBound.height<43||!button.text.Contains("%"))throw new Exception("Player row missing or too small");}
            if(Root.Query<Label>(className:"interlude-value").ToList().Count!=12)throw new Exception("Live statistics missing");
            var stats=Root.Q(className:"interlude-stats");foreach(var row in stats.Query<VisualElement>(className:"interlude-stat").ToList())if(row.worldBound.height<24||row.worldBound.yMax>stats.worldBound.yMax+1)throw new Exception("Statistics collapsed or escaped their card");
        }
        static void Finish(bool passed)
        {
            int previousView=SessionState.GetInt("BroadcastPreviousView",-1);if(previousView<0)PlayerPrefs.DeleteKey("match-view-mode");else PlayerPrefs.SetInt("match-view-mode",previousView);PlayerPrefs.SetInt("match-highlights",SessionState.GetInt("BroadcastPreviousMode",1));
            PlayerPrefs.SetInt("match-camera-mode",SessionState.GetInt("BroadcastPreviousCamera",0));PlayerPrefs.SetInt("match-live-speed",SessionState.GetInt("BroadcastPreviousSpeed",1));PlayerPrefs.SetFloat("match-camera-zoom",SessionState.GetFloat("BroadcastPreviousZoom",1));
            SessionState.SetBool("MatchBroadcastSmoke",false);EditorApplication.Exit(passed?0:1);
        }
        static void Tick()
        {
            if(!SessionState.GetBool("MatchBroadcastSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<20)return;frames=0;
            try{
                string requested=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineBroadcastOutput="))?.Split('=')[1]??"broadcast";if(!System.Text.RegularExpressions.Regex.IsMatch(requested,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output name");output=Path.GetFullPath("../../artifacts/unity/"+requested);Directory.CreateDirectory(output);var app=TouchlineApp.Instance;
                switch(stage++){
                    case 0:Resize(1280,966);app.Career.life.day=app.Career.life.nextFixture;Click("Match");Click("Entrer sur le terrain");break;
                    case 1:
                        if(Root.Query<Button>().ToList().Any(b=>b.text=="Coup d’envoi"))Click("Coup d’envoi");arena=UnityEngine.Object.FindFirstObjectByType<MatchArena>();arena.enabled=false;arena.Broadcast.Enabled=true;arena.Paused=false;
                        int count=0;while(!arena.QuietPresentation&&count++<1200)arena.RenderFrame(.1f);if(!arena.QuietPresentation)throw new Exception("No natural calm phase found");arena.Paused=true;break;
                    case 2:CheckDashboard();Capture("calm-fold-open");selected=app.Career.match.actors[0].id;pausedClock=app.Career.match.clock;Click("interlude-player-0");break;
                    case 3:
                        if(!arena.Paused||Root.Q("player-profile")==null||app.Career.match.clock!=pausedClock)throw new Exception("Player profile must pause the calm clock");Capture("calm-player-profile");Click("Fermer");Resize(1080,2520);break;
                    case 4:CheckDashboard();Capture("calm-portrait");Resize(1600,700);break;
                    case 5:CheckDashboard();Capture("calm-landscape");Click("Tactique");break;
                    case 6:Capture("calm-tactics");Click("tactics-return-match");break;
                    case 7:pausedClock=app.Career.match.clock;Click("match-bench-open");break;
                    case 8:
                        var bench=Root.Q("match-bench");if(bench==null||bench.worldBound.width>Root.worldBound.width||!arena.Paused)throw new Exception("Bench layout or pause failed");Capture("bench-landscape");
                        var enter=bench.Query<Button>().ToList().First(b=>b.name.StartsWith("bench-enter-")&&b.enabledSelf);selected=enter.name.Substring("bench-enter-".Length);Click(enter.name);Resize(1080,2520);break;
                    case 9:
                        if(app.Career.match.clock!=pausedClock||!app.Career.match.pendingSubstitutions.Any(p=>p.incoming==selected))throw new Exception("Bench did not queue a substitution while paused");Capture("bench-pending");Click("bench-cancel");if(app.Career.match.pendingSubstitutions.Count!=0)throw new Exception("Bench cancellation failed");Click("Fermer");Resize(1600,700);break;
                    case 10:if(!arena.Paused)throw new Exception("Tactics return resumed the clock");Click("match-broadcast-mode");Click("match-view-Full");arena.RenderFrame(.02f);break;
                    case 11:
                        if(arena.Broadcast.Enabled||!arena.MatchCamera.enabled||Root.Q("match-viewport").resolvedStyle.display==DisplayStyle.None)throw new Exception("Integral view did not restore 3D");Capture("integral-return");Click("match-broadcast-mode");Click("match-view-Highlights");
                        var m=app.Career.match;m.clock=100;m.phase="throw-in";m.restart=20;m.ball=new BallState{position=new Point(0,34),previous=new Point(0,34)};arena.Broadcast.Refresh();arena.RenderFrame(.02f);break;
                    case 12:
                        if(!arena.QuietPresentation)throw new Exception("Calm fixture did not select dashboard");arena.Paused=false;float before=app.Career.match.clock;arena.RenderFrame(.1f);if(app.Career.match.clock-before<.75f)throw new Exception("Calm clock did not accelerate");arena.Paused=true;
                        app.Career.match.phase="corner";app.Career.match.restart=5;app.Career.match.restartSide=0;app.Career.match.ball.position=new Point(52.5f,34);arena.RenderFrame(.02f);break;
                    case 13:
                        if(arena.QuietPresentation||!arena.MatchCamera.enabled)throw new Exception("Dangerous restart must return to 3D");var ballPoint=app.Career.match.ball.position;var projected=arena.MatchCamera.WorldToViewportPoint(new Vector3(ballPoint.x,.11f,ballPoint.z));Debug.Log("BROADCAST_CAMERA position="+arena.MatchCamera.transform.position+" ball="+ballPoint.x+","+ballPoint.z+" projection="+projected+" aspect="+arena.MatchCamera.aspect);if(projected.x<.08f||projected.x>.92f||projected.y<.08f||projected.y>.92f)throw new Exception("Actual ball is outside the broadcast safe frame");Capture("corner-live-return");
                        app.Career.match.clock=app.Career.match.HalfDuration-.1f;app.Career.match.phase="throw-in";app.Career.match.restart=20;arena.Paused=false;arena.RenderFrame(.1f);break;
                    case 14:
                        if(!app.Career.match.halfTime||!arena.Paused||app.Career.match.clock!=app.Career.match.HalfDuration)throw new Exception("Half-time boundary crossed");Capture("half-time");
                        arena.CameraMode();Resize(1080,2520);for(int i=0;i<90;i++)arena.RenderFrame(.02f);break;
                    case 15:
                        foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1}){var p=arena.MatchCamera.WorldToViewportPoint(new Vector3(x*52.5f,0,z*34));if(p.z<=0||p.z>=arena.MatchCamera.farClipPlane||p.x<.08f||p.x>.92f||p.y<.08f||p.y>.92f)throw new Exception("Tactical portrait pitch is clipped");}Capture("tactical-portrait");
                        pausedClock=app.Career.match.clock;Click("Options du match");break;
                    case 16:
                        var options=Root.Q("match-options");if(options==null)throw new Exception("Options missing");
                        Root.Q<DropdownField>("match-camera-choice").index=0;Root.Q<DropdownField>("match-live-speed").index=1;Root.Q<Toggle>("match-tactical-overlay").value=true;Click("match-zoom-in");Click("match-zoom-out");
                        if(Root.Q("match-options")!=options||!arena.Paused||app.Career.match.clock!=pausedClock||arena.Speed!=2||arena.TacticalCamera||!arena.TacticalOverlayVisible)throw new Exception("Settings must stay open, apply and preserve pause");break;
                    case 17:
                        Capture("match-options-portrait");Click("Fermer");Click("Options du match");break;
                    case 18:
                        if(Root.Q<DropdownField>("match-live-speed").index!=1||!Root.Q<Toggle>("match-tactical-overlay").value)throw new Exception("Reopened settings lost current state");
                        Resize(1600,700);break;
                    case 19:
                        var resume=Root.Q<Button>("match-options-resume");var settings=Root.Q<ScrollView>("match-settings");
                        if(resume==null||settings==null||resume.worldBound.yMax>Root.worldBound.yMax||resume.worldBound.yMin<0||settings.worldBound.height<60||settings.worldBound.yMax>resume.worldBound.yMin+2)throw new Exception("Landscape settings must scroll above a visible resume button");
                        Capture("match-options-landscape");settings.scrollOffset=new Vector2(0,10000);break;
                    case 20:
                        if(Root.Q<ScrollView>("match-settings").scrollOffset.y<=0)throw new Exception("Landscape settings cannot scroll");Capture("match-options-landscape-bottom");
                        Click("Fermer");Click("match-broadcast-mode");Resize(1280,966);break;
                    case 21:Capture("viewing-modes-fold");Click("match-view-KeyMoments");if(arena.Broadcast.Mode!=MatchViewingMode.KeyMoments||!arena.Paused||PlayerPrefs.GetInt("match-view-mode",-1)!=1)throw new Exception("Key mode or pause not preserved");Click("Options du match");break;
                    case 22:if(Root.Q<DropdownField>("match-viewing-mode").index!=0)throw new Exception("Options lost key mode");Root.Q<DropdownField>("match-viewing-mode").index=2;if(arena.Broadcast.Mode!=MatchViewingMode.Extended)throw new Exception("Extended mode not applied");Click("Fermer");Click("match-broadcast-mode");Resize(1080,2520);break;
                    case 23:Capture("viewing-modes-portrait");Click("match-view-Full");Click("Options du match");break;
                    case 24:if(Root.Q<DropdownField>("match-viewing-mode").index!=3)throw new Exception("Complete match preference lost");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"viewingModes\":4,\"viewingPreference\":true,\"naturalCalm\":true,\"stats\":true,\"bothLineups\":true,\"touchTargets\":true,\"profilePause\":true,\"tacticsReturn\":true,\"integralSwitch\":true,\"acceleratedClock\":true,\"dangerReturnsTo3D\":true,\"halfTimeStop\":true,\"portraitLandscape\":true,\"benchQueueAndCancel\":true,\"tacticalPortrait\":true,\"persistentSettingsPanel\":true,\"physicalAndroid\":false}");Debug.Log("TOUCHLINE_BROADCAST_OK");Finish(true);break;
                }
            }catch(Exception e){Debug.LogException(e);Finish(false);}
        }
    }
}
