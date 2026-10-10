using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    // Synthetic trajectory preview: real rendered rig, no personal save and no simulated match result.
    [InitializeOnLoad] public static class KeeperClaimReview
    {
        const string Key="KeeperClaimReview";
        static int frame,last=-1;static PlayerView view;static Actor actor;static MatchState state;
        static Camera camera;static RenderTexture target;static Transform ball;static string output;
        static KeeperClaimReview(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static void Setup()
        {
            var app=TouchlineApp.Instance;
            var safe=typeof(TouchlineApp).GetProperty("VisualValidation",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            if(!(bool)safe.GetValue(app))throw new Exception("Personal saves must be isolated");
            string name=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlineClaim="))?.Substring(16)??"claim-review";
            if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9.-]+$"))throw new Exception("Invalid output name");
            output=Path.GetFullPath("build/film/"+name);if(Directory.Exists(output))throw new Exception("Preserve existing proof");Directory.CreateDirectory(output);
            app.GetComponent<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
            actor=new Actor{id="review-keeper",slot=0,side=0,action="idle",position=new Point(-49,0),previous=new Point(-49,0),angle=Mathf.PI*.5f};
            state=new MatchState{period=1,restart=0,phase="play",actors=new[]{actor},ball=new BallState{kind="cross",side=1,from="crosser",start=new Point(-40,0),end=new Point(-50,0),duration=1,startHeight=2.2f,endHeight=2.2f}};
            var go=new GameObject("Keeper claim preview");view=go.AddComponent<PlayerView>();view.Build(new PlayerData{id=actor.id,heightCm=182},0,0,Color.yellow);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.position=new Vector3(-49,0,0);ground.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(new Color(.10f,.24f,.13f));
            var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(45,-35,0);RenderSettings.ambientLight=Color.gray;
            camera=new GameObject("Keeper review camera").AddComponent<Camera>();camera.transform.position=new Vector3(-45,2.5f,4);camera.transform.LookAt(new Vector3(-48.6f,1.1f,0));camera.fieldOfView=37;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.09f,.13f);
            target=new RenderTexture(1280,720,24);target.Create();camera.targetTexture=target;
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball=sphere.transform;ball.localScale=Vector3.one*.22f;sphere.GetComponent<Renderer>().sharedMaterial=PlayerView.Material(Color.white);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null||last==Time.frameCount)return;last=Time.frameCount;
            try{
                if(view==null){Setup();return;}
                const float dt=1f/60;float t=frame*dt;state.ball.elapsed=.45f+t;
                var sample=KeeperClaimAnticipation.Evaluate(state,actor,1);
                var point=Point.Lerp(state.ball.start,state.ball.end,Mathf.Clamp01(state.ball.elapsed));ball.position=new Vector3(point.x,2.2f,point.z);
                view.Render(actor,1,dt,ball.position,new PlayerMotionContext{keeperClaim=sample});
                camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,"frame-"+frame.ToString("D3")+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
                if(++frame>=21){File.WriteAllText(Path.Combine(output,"scope.txt"),"Synthetic airborne claim readiness, 0.35 s at60Hz,182cm keeper. No catch outcome, no personal save, no Android performance measurement.");SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
            }catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
