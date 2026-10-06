using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class PlayerEvidenceReview
    {
        static int stage,frames,last=-1;static string output,player;static RenderTexture target;
        static VisualElement Root=>TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;
        static PlayerEvidenceReview(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("PlayerEvidenceReview",true);EditorApplication.isPlaying=true;}
        static void Click(string text){var b=Root.Query<Button>().ToList().First(x=>x.text==text);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int width,int height){var old=target;target=new RenderTexture(width,height,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
        static void ScrollEvidence(){Root.Q<ScrollView>("profile-provenance").ScrollTo(Root.Q("player-performance-evidence"));}
        static void Tick()
        {
            if(!SessionState.GetBool("PlayerEvidenceReview",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;if(++frames<20)return;frames=0;
            try{
                var app=TouchlineApp.Instance;
                switch(stage++){
                    case 0:
                        string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineEvidenceOutput="))?.Split('=')[1]??"player-evidence-review-v1";
                        if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9-]+$"))throw new Exception("Invalid output name");
                        output=Path.GetFullPath("../../artifacts/unity/"+name);if(Directory.Exists(output))throw new Exception("Preserve prior review");Directory.CreateDirectory(output);
                        var evidence=JsonUtility.FromJson<DataProvenance>(Resources.Load<TextAsset>("Data/data-provenance").text);
                        player=evidence.players.First(p=>p.performanceEvidence?.Length>0&&app.Database.Find(p.id).team!=app.Career.club).id;
                        app.Career.EnsureWorld(app.Database);app.Career.revealAttributes=false;app.Career.world.reports.RemoveAll(r=>r.player==player);
                        app.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(app.GetComponent<UIDocument>().panelSettings);Resize(1280,900);app.PlayerProfile(player);Click("Sources");break;
                    case 1:ScrollEvidence();break;
                    case 2:
                        var card=Root.Q("player-performance-evidence");if(card==null)throw new Exception("Public measured speed missing");
                        string text=string.Join(" ",card.Query<Label>().ToList().Select(l=>l.text));
                        if(!text.Contains("km/h")||!text.Contains("2025/26")||!text.Contains("05/10/2026")||!text.Contains("estimation"))throw new Exception("Missing source, date or distinction from estimate");
                        Capture("evidence-fold");Resize(420,900);break;
                    case 3:ScrollEvidence();break;
                    case 4:Capture("evidence-phone");Click("Attributs");break;
                    case 5:
                        if(app.Career.Knowledge(player)>=40)throw new Exception("Public evidence changed scouting knowledge");
                        if(Root.Query<Label>(className:"attribute-score").ToList().Any(l=>l.text.Any(char.IsDigit)))throw new Exception("Hidden ability leaked");
                        Capture("hidden-attributes");File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"screens\":3,\"physicalAndroid\":false,\"player\":\""+player+"\"}");
                        SessionState.SetBool("PlayerEvidenceReview",false);Debug.Log("TOUCHLINE_PLAYER_EVIDENCE_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("PlayerEvidenceReview",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
