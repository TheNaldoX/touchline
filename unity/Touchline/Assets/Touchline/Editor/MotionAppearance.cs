using UnityEngine;
using Touchline.Core;

namespace Touchline.Editor
{
    public static partial class MotionCapture
    {
        static int appearanceFrame;
        static bool CaptureAppearance()
        {
            if(appearanceFrame>=240){views[7].gameObject.SetActive(true);return true;}
            if(appearanceFrame>=180){
                var ball=footballs[8];
                if(appearanceFrame==180){views[7].gameObject.SetActive(false);ball.gameObject.SetActive(true);ball.GetComponent<MeshFilter>().sharedMesh=FootballBallMesh.Shared;var renderer=ball.GetComponent<MeshRenderer>();renderer.sharedMaterials=new[]{renderer.sharedMaterial,PlayerView.Material(new Color(.025f,.032f,.04f))};}
                ball.position=new Vector3(0,.11f,0);ball.rotation=Quaternion.Euler(0,(appearanceFrame-180)*4,0);
                camera.transform.position=new Vector3(.28f,.28f,.5f);camera.transform.LookAt(ball.position);camera.fieldOfView=32;
                if(appearanceFrame==210)SaveCapture("ball-detail");appearanceFrame++;return false;
            }
            int example=appearanceFrame/60,phase=appearanceFrame%60;
            if(phase==0){
                foreach(var ball in footballs)if(ball!=null)ball.gameObject.SetActive(false);
                views[7].ChangeIdentity(new PlayerData{id="face-"+example,heightCm=182});views[7].transform.rotation=Quaternion.identity;
                actors[7].position=actors[7].previous=new Point();actors[7].velocity=new Point();actors[7].action="idle";actors[7].actionTime=0;actors[7].angle=0;actors[7].slot=9;
            }
            views[7].Render(actors[7],1,1f/60,new Vector3(0,1.7f,10));
            camera.transform.position=new Vector3(.30f,1.76f,1.05f);camera.transform.LookAt(new Vector3(0,1.65f,0));camera.fieldOfView=32;
            if(phase==30)SaveCapture("appearance-"+example);appearanceFrame++;return false;
        }
    }
}
