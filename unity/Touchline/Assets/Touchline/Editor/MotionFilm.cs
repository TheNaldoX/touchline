using System;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Editor
{
    public static partial class MotionCapture
    {
        static int filmFrame;static MediaEncoder film;static Texture2D filmTexture;
        static readonly BallVisualRotation filmBallRotation=new BallVisualRotation();
        static void CaptureFilm()
        {
            if(filmFrame==0){
                film=new MediaEncoder(Path.Combine(output,"locomotion-sequence.mp4"),new VideoTrackAttributes{frameRate=new MediaRational(30),width=(uint)target.width,height=(uint)target.height,includeAlpha=false},Array.Empty<AudioTrackAttributes>());
                filmTexture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))if(renderer.name=="Plane")renderer.transform.localScale=new Vector3(20,1,20);
                views[7].ChangeIdentity(new PlayerData{id="sequence-athlete",heightCm=180});actors[7].position=actors[7].previous=new Point();actors[7].stride=0;actors[7].actionSequence=0;actors[7].slot=9;actors[7].injured=false;
            }else{
                var previous=RenderTexture.active;RenderTexture.active=target;filmTexture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);filmTexture.Apply();RenderTexture.active=previous;
                if(!film.AddFrame(filmTexture))throw new InvalidOperationException("Cannot encode motion film frame");
                if(filmFrame%60==0||filmFrame>=720&&(filmFrame%60==6||filmFrame%60==15||filmFrame%60==27))SaveCapture("sequence-"+filmFrame.ToString("D3"));
            }
            if(filmFrame>=1440){film.Dispose();UnityEngine.Object.Destroy(filmTexture);SessionState.SetBool("TouchlineMotionCapture",false);Debug.Log("TOUCHLINE_MOTION_CAPTURE_OK");EditorApplication.Exit(0);return;}
            var actor=actors[7];float t=filmFrame/30f;actor.previous=actor.position;actor.action="run";actor.actionTime=0;actor.actionKind=null;actor.angle=0;actor.injured=false;
            var context=new PlayerMotionContext();
            if(t<4)actor.velocity=new Point(0,Mathf.SmoothStep(.5f,3,t/4));
            else if(t<8)actor.velocity=new Point(Mathf.Sin((t-4)*Mathf.PI*.5f)*2.5f,-Mathf.Cos((t-4)*Mathf.PI*.5f)*2.5f);
            else if(t<10){actor.velocity=new Point();actor.action="idle";actor.angle=(t-8)*Mathf.PI*.5f;}
            else if(t<14)actor.velocity=new Point(0,Mathf.SmoothStep(0,6,(t-10)/2));
            else if(t<18){actor.velocity=new Point(0,2);context.carrying=true;}
            else if(t<18.64f){actor.velocity=new Point();actor.action="kick";actor.actionKind="pass";actor.actionTime=.64f-(t-18);actor.actionTarget=actor.position+new Point(0,.42f);actor.actionHeight=.11f;actor.actionSequence=1;}
            else if(t<21){actor.velocity=new Point(0,.8f);actor.injured=true;}
            else if(t<24){actor.velocity=new Point();actor.action="idle";context.reaction=2;context.reactionTime=t-21;}
            else if(t<36){
                int example=(filmFrame-720)/60;float phase=((filmFrame-720)%60)/30f;int side=example%2==0?1:-1;
                if((filmFrame-720)%60==0){views[7].ChangeIdentity(new PlayerData{id="keeper-film-"+example,heightCm=185});actor.position=actor.previous=new Point();}
                actor.slot=0;actor.velocity=new Point();actor.action=phase<1.2f?"dive":"idle";actor.actionTime=Mathf.Max(0,1.2f-phase);actor.actionKind=example>=4?"save-parry":"save-catch";actor.actionContactTime=.18f;actor.actionSequence=example+1;actor.diveSide=side;actor.actionTarget=new Point(side*1.1f,.3f);actor.actionHeight=example<2?.22f:example<4?1.1f:2.2f;
            }else if(t<40){
                float phase=((filmFrame-1080)%60)/30f;string kind=t<38?"keeper-roll":"keeper-throw";
                if((filmFrame-1080)%60==0){views[7].ChangeIdentity(new PlayerData{id="distribution-film-"+kind,heightCm=182});actor.position=actor.previous=new Point();}
                actor.slot=0;actor.velocity=new Point();actor.action=phase<MatchSimulation.KeeperDistributionDuration?kind:"idle";actor.actionKind=kind;actor.actionTime=Mathf.Max(0,MatchSimulation.KeeperDistributionDuration-phase);actor.actionContactTime=.48f;actor.actionSequence=8+(t<38?0:1);actor.actionTarget=new Point(-.20f,.52f);actor.actionHeight=t<38?.24f:1.78f;
            }else if(t<44){
                if(filmFrame==1200){views[7].ChangeIdentity(new PlayerData{id="defensive-film",heightCm=182});actor.position=actor.previous=new Point();}
                actor.slot=2;actor.intent="mark";actor.velocity=t<42?new Point(0,-2):new Point(2,0);actor.angle=t<42?Mathf.PI:Mathf.PI*.5f;context.defending=true;
            }else{
                float phase=((filmFrame-1320)%60)/30f;int side=t<46?1:-1;
                if((filmFrame-1320)%60==0){views[7].ChangeIdentity(new PlayerData{id="duel-film-"+side,heightCm=180});actor.position=actor.previous=new Point();}
                actor.slot=2;actor.velocity=new Point();actor.intent="press";actor.action=phase<.75f?"tackle":"idle";actor.actionTime=Mathf.Max(0,.75f-phase);actor.actionKind=MatchSimulation.StandingDuel;actor.actionContactTime=.2f;actor.actionSequence=12+side;actor.actionTarget=new Point(side*.85f,.42f);actor.actionHeight=.11f;
            }
            actor.position+=actor.velocity/30;actor.stride+=actor.velocity.Length/30;
            var ball=new Vector3(actor.position.x,.11f,actor.position.z+.30f+.26f*(.5f+.5f*Mathf.Sin(actor.stride*2.4f)));
            if(actor.action=="kick")ball=new Vector3(actor.actionTarget.x,.11f,actor.actionTarget.z+Mathf.Max(0,t-18.18f)*7);
            if(t>=24&&t<36){float phase=((filmFrame-720)%60)/30f;ball=new Vector3(actor.actionTarget.x,actor.actionHeight,.3f)+Vector3.forward*Mathf.Max(0,.18f-phase)*6;if(actor.actionKind=="save-parry"&&phase>.18f)ball+=new Vector3(actor.diveSide*4,1,2)*(phase-.18f);}
            if(t>=36&&t<40)ball=DistributionFilmBall(new BallState{kind=actor.actionKind,setupStart=new Point(0,.36f),setupHeight=1.1f,start=actor.actionTarget,startHeight=actor.actionHeight,end=new Point(0,18)},((filmFrame-1080)%60)/30f);
            if(t>=40&&t<44)ball=new Vector3(0,.11f,9);
            if(t>=44){float phase=((filmFrame-1320)%60)/30f;int side=t<46?1:-1;ball=new Vector3(side*.85f,.11f,.42f)+new Vector3(side*2,0,1)*Mathf.Max(0,phase-.2f);}
            views[7].Render(actor,1,1f/30,ball,context);footballs[8].gameObject.SetActive(t>=14&&t<18.64f||t>=24);
            if(t>=24&&actor.actionKind=="save-catch"&&(filmFrame-720)%60>=6)ball=views[7].HeldBallPosition;footballs[8].position=ball;
            filmBallRotation.Advance(ball,t>=24&&actor.actionKind=="save-catch"&&(filmFrame-720)%60>=6,1f/30);footballs[8].rotation=filmBallRotation.Rotation;
            var center=new Vector3(actor.position.x,1,actor.position.z);camera.transform.position=center+new Vector3(3.1f,1.1f,4);camera.transform.LookAt(center);filmFrame++;
        }
        static Vector3 DistributionFilmBall(BallState setup,float t)
        {
            if(t<=.48f){var point=MatchSimulation.KeeperDistributionPreparation(setup,t/.48f,out float height);return new Vector3(point.x,height,point.z);}
            float u=Mathf.Clamp01((t-.48f)/(setup.kind=="keeper-roll"?1.5f:1));var p=Point.Lerp(setup.start,setup.end,u);float y=Mathf.Lerp(setup.startHeight,.11f,u)+Mathf.Sin(u*Mathf.PI)*(setup.kind=="keeper-roll"?.035f:2.1f);return new Vector3(p.x,y,p.z);
        }
    }
}
