using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class MatchPlayerLabelsSmoke
    {
        static int stage,frames,last=-1,identityUpdates;static string selected,incoming,output;static RenderTexture target;
        static TouchlineApp App=>TouchlineApp.Instance;
        static VisualElement Root=>App.GetComponent<UIDocument>().rootVisualElement;
        static MatchArena Arena=>UnityEngine.Object.FindFirstObjectByType<MatchArena>();
        static MatchPlayerLabels Names=>Arena.Viewport.PlayerLabels;
        static MatchPlayerLabelsSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){SessionState.SetInt("LabelSmokeNames",PlayerPrefs.GetInt("match-player-names",1));SessionState.SetInt("LabelSmokeCamera",PlayerPrefs.GetInt("match-camera-mode",0));SessionState.SetInt("LabelSmokeSpeed",PlayerPrefs.GetInt("match-live-speed",1));SessionState.SetFloat("LabelSmokeZoom",PlayerPrefs.GetFloat("match-camera-zoom",1));PlayerPrefs.SetInt("match-player-names",2);PlayerPrefs.SetInt("match-camera-mode",0);PlayerPrefs.SetInt("match-live-speed",1);PlayerPrefs.SetFloat("match-camera-zoom",1);SessionState.SetBool("BallSmokeHadPreference",PlayerPrefs.HasKey("match-ball-locator"));SessionState.SetInt("BallSmokePreference",PlayerPrefs.GetInt("match-ball-locator",1));PlayerPrefs.SetInt("match-ball-locator",1);ProjectBuilder.Configure();SessionState.SetBool("MatchPlayerLabelsSmoke",true);EditorApplication.isPlaying=true;}
        static void Finish(bool passed){PlayerPrefs.SetInt("match-player-names",SessionState.GetInt("LabelSmokeNames",1));PlayerPrefs.SetInt("match-camera-mode",SessionState.GetInt("LabelSmokeCamera",0));PlayerPrefs.SetInt("match-live-speed",SessionState.GetInt("LabelSmokeSpeed",1));PlayerPrefs.SetFloat("match-camera-zoom",SessionState.GetFloat("LabelSmokeZoom",1));if(SessionState.GetBool("BallSmokeHadPreference",false))PlayerPrefs.SetInt("match-ball-locator",SessionState.GetInt("BallSmokePreference",1));else PlayerPrefs.DeleteKey("match-ball-locator");SessionState.SetBool("MatchPlayerLabelsSmoke",false);EditorApplication.Exit(passed?0:1);}
        static void Click(string name){var button=Root.Query<Button>().ToList().FirstOrDefault(b=>b.name==name||b.text==name);if(button==null)throw new Exception("Missing "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();App.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
        static void CheckVisible(){if(Names.VisibleCount<4||Names.Layer.childCount!=22)throw new Exception("Player name pool missing or invisible");var size=Root.Q<Image>("match-viewport").contentRect.size;for(int i=0;i<22;i++)if(Names.VisibleAt(i)){var b=Names.BoundsAt(i);if(b.x<0||b.y<0||b.xMax>size.x+.1f||b.yMax>size.y+.1f)throw new Exception("Name outside viewport");if(Names.LabelAt(i).text!=Arena.PlayerSurname(Arena.Simulation.State.actors[i].id))throw new Exception("Name belongs to wrong player");for(int j=0;j<i;j++)if(Names.VisibleAt(j)&&b.Overlaps(Names.BoundsAt(j)))throw new Exception("Visible names overlap");}}
        static void CheckLocator(bool expected)
        {
            for(int i=0;i<22;i++)if(Names.VisibleAt(i)){
                var label=Names.LabelAt(i);float required=label.MeasureTextSize(label.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x+12;
                if(required<=180&&Names.BoundsAt(i).width+.5f<Mathf.Max(44,required))throw new Exception("A short surname is truncated despite available label space: "+label.text+" required="+required+" cached="+Names.BoundsAt(i).width+" font="+label.resolvedStyle.fontSize);
            }
            var locator=Arena.Viewport.BallLocator;
            if(locator==null||locator.Visible!=expected)throw new Exception("Ball locator visibility did not follow live/quiet/off state");
            if(!expected)return;
            var image=Root.Q<Image>("match-viewport");var point=Arena.MatchCamera.WorldToViewportPoint(Arena.BallDisplayPosition);var size=image.contentRect.size;
            var centre=new Vector2(point.x*size.x,(1-point.y)*size.y);
            if(Vector2.Distance(locator.Bounds.center,centre)>.5f)throw new Exception("Ball locator does not mark the actual rendered ball");
            if(locator.Marker.pickingMode!=PickingMode.Ignore||locator.Layer.pickingMode!=PickingMode.Ignore)throw new Exception("Ball locator intercepts touch input");
            var picked=Root.panel.Pick(image.LocalToWorld(centre+image.contentRect.position));
            if(picked!=image)throw new Exception("The ball overlay changed the viewport's touch target");
            if(locator.Bounds.width!=(Arena.BallDisplay==MatchBallDisplay.Discreet?10:18))throw new Exception("Ball locator changed size with camera zoom or rotation");
        }
        static void Tick()
        {
            if(!SessionState.GetBool("MatchPlayerLabelsSmoke",false)||!EditorApplication.isPlaying||App==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<24)return;frames=0;
            try{
                output=Path.GetFullPath("../../artifacts/unity/player-labels-0.33");Directory.CreateDirectory(output);
                switch(stage++){
                    case 0:App.Career.EnsureWorld(App.Database);Resize(1280,966);App.Career.life.day=App.Career.life.nextFixture;Click("Match");Click("Entrer sur le terrain");break;
                    case 1:Arena.Broadcast.Enabled=false;Arena.Paused=true;Arena.RenderFrame(1f/60);break;
                    case 2:CheckVisible();CheckLocator(true);Capture("names-unfolded");identityUpdates=Names.IdentityUpdates;
                        for(int i=21;i>=0;i--)if(Names.VisibleAt(i)){var center=Names.BoundsAt(i).center;if(Names.FindAt(center)!=Names.IdentityAt(i))continue;selected=Names.IdentityAt(i);var size=Root.Q<Image>("match-viewport").contentRect.size;if(Arena.Viewport.SelectAt(new Vector2(center.x/size.x,1-center.y/size.y))!=selected)throw new Exception("Name tap failed");break;}
                        if(selected==null)throw new Exception("No name can be tapped");break;
                    case 3:if(!Root.Q("player-profile").Query<Label>().ToList().Any(l=>l.text==App.Database.Find(selected).name))throw new Exception("Name opens incorrect full profile");Capture("name-player-profile");Click("Fermer");Arena.SetBallDisplay(MatchBallDisplay.Enhanced);Arena.ScaleZoom(.8f);Arena.CameraMode();Resize(1080,2520);break;
                    case 4:CheckVisible();CheckLocator(true);Capture("names-folded-zoom");if(Names.IdentityUpdates!=identityUpdates)throw new Exception("Names rebuilt every frame or rotation");
                        incoming=App.Database.Squad(Arena.Simulation.State.home).First(p=>!Arena.Simulation.State.used.Contains(p.id)&&p.unavailableDays==0).id;Arena.Simulation.Substitute(0,9,incoming);break;
                    case 5:if(Names.IdentityAt(9)!=incoming||Names.IdentityUpdates!=identityUpdates+1)throw new Exception("Substitution did not refresh exactly one identity");Arena.Simulation.State.actors[10].sentOff=true;break;
                    case 6:if(Names.VisibleAt(10))throw new Exception("Excluded player still labelled");var state=Arena.Simulation.State;state.clock=20;state.phase="throw-in";state.restart=20;state.ball=new BallState{position=new Point(0,34),previous=new Point(0,34)};Arena.Broadcast.Enabled=true;Arena.Broadcast.Refresh();break;
                    case 7:CheckLocator(false);if(!Arena.QuietPresentation||Names.VisibleCount!=0||Names.Layer.resolvedStyle.display!=DisplayStyle.None)throw new Exception("Labels remain over quiet statistics");Capture("names-hidden-interlude");Arena.Broadcast.Enabled=false;Arena.RenderFrame(1f/60);Resize(1600,700);break;
                    case 8:CheckVisible();CheckLocator(true);Capture("names-landscape-restored");Arena.SetNameDisplay(MatchNameDisplay.Off);Arena.SetBallDisplay(MatchBallDisplay.Off);break;
                    case 9:CheckLocator(false);if(Names.VisibleCount!=0||Names.Layer.resolvedStyle.display!=DisplayStyle.None)throw new Exception("Disabled names still visible");Capture("names-disabled");Click("Options du match");Root.Q<DropdownField>("match-player-names").index=1;Root.Q<DropdownField>("match-ball-locator").index=2;Root.Q<DropdownField>("match-live-speed").index=4;break;
                    case 10:if(Arena.BallDisplay!=MatchBallDisplay.Enhanced||PlayerPrefs.GetInt("match-ball-locator")!=2)throw new Exception("Ball locator option did not persist");if(Arena.NameDisplay!=MatchNameDisplay.NearBall||Arena.Speed!=10||PlayerPrefs.GetInt("match-player-names")!=1||Names.VisibleCount>6)throw new Exception("Name mode or express speed did not persist");Capture("name-speed-options");Click("Fermer");Arena.Simulation.State.events.Add(new MatchEvent{kind="shot",side=0,player=Arena.Simulation.State.actors[9].id,time=Arena.Simulation.State.clock,text="Frappe test"});break;
                    case 11:CheckLocator(true);if(Root.Q<Label>(className:"match-signal-title").text!="FRAPPE"||Root.Q("match-action-signal").resolvedStyle.display==DisplayStyle.None)throw new Exception("Shot signal missing");Capture("shot-feedback");Arena.Simulation.State.score[0]++;Arena.Simulation.State.events.Add(new MatchEvent{kind="goal",side=0,player=Arena.Simulation.State.actors[9].id,time=Arena.Simulation.State.clock,text="But test"});break;
                    case 12:if(!Root.Q<Label>(className:"match-signal-title").text.StartsWith("BUT !")||Root.Q("match-action-signal").resolvedStyle.display==DisplayStyle.None)throw new Exception("Goal signal missing");Capture("goal-feedback");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"pool\":22,\"surnames\":true,\"noVisibleOverlap\":true,\"namesOffNearAll\":true,\"speed10\":true,\"shotGoalSignals\":true,\"nativeUiProjection\":true,\"ballLocatorProjection\":true,\"ballLocatorOffDiscreetEnhanced\":true,\"ballLocatorQuietHidden\":true,\"ballLocatorPickingIgnored\":true,\"nameTapProfile\":true,\"pausedZoomRotation\":true,\"substitution\":true,\"sentOffHidden\":true,\"quietHidden\":true,\"identityUpdatesOnly\":true,\"physicalAndroid\":false}");Debug.Log("TOUCHLINE_PLAYER_LABELS_OK");Finish(true);break;
                }
            }catch(Exception e){Debug.LogException(e);Finish(false);}
        }
    }
}


