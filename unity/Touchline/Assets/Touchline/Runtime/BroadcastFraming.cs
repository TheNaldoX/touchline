using UnityEngine;

namespace Touchline
{
    public static class BroadcastFraming
    {
        // Fit real world points using the actual viewport aspect ratio.
        // Portrait, unfolded and ultra-wide layouts must retain the ball.
        // Pente de la caméra télé (descente par mètre d'avancée) : 0,58 ≈ 30°, proche des plans de diffusion.
        public const float BroadcastPitch=.58f;
        const float GoalApproachStart=18f,GoalApproachFull=28f; // m depuis le milieu : ouverture progressive avant le dernier tiers
        const float GoalFocusStart=39f,GoalFocusFull=29f;       // m entre le regard et la ligne de but
        const float PortraitBallWide=24f,PortraitBallCentral=18f,PortraitFocusWide=18f,PortraitFocusCentral=12f; // m du centre en largeur
        public static void Apply(Camera camera,Vector3 focus,Vector3 ball,bool wide,bool goal,int direction,float zoom)
        {
            bool portrait=camera.aspect<.8f;
            var forward=portrait?new Vector3(0,-1,-.08f).normalized:new Vector3(-.035f,-BroadcastPitch,-1).normalized;
            var rotation=Quaternion.LookRotation(forward,portrait?Vector3.right:Vector3.up);
            var right=rotation*Vector3.right;var up=rotation*Vector3.up;
            float vertical=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)*.78f;
            float horizontal=vertical*Mathf.Max(.2f,camera.aspect);
            zoom=Mathf.Clamp(zoom,.7f,1.4f);
            float distance=(wide?70:46)*zoom;
            if(!wide){
                // Keep a stable view of the passing lanes around the ball.
                // Portrait follows the length of the pitch, never a tiny landscape strip.
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Fit(focus+new Vector3(x*18*zoom,0,z*12*zoom));
            }
            Fit(ball);Fit(ball+Vector3.up*2.2f);
            // In portrait a corner and the whole goal cannot both fill a
            // readable frame. Follow the taker, then follow delivery centrally.
            if(goal&&!wide){
                float blend=Ramp(GoalApproachStart,GoalApproachFull,Mathf.Abs(ball.x))*Ramp(GoalFocusStart,GoalFocusFull,Mathf.Abs(direction*52.5f-focus.x));
                if(portrait)blend*=Ramp(PortraitBallWide,PortraitBallCentral,Mathf.Abs(ball.z))*Ramp(PortraitFocusWide,PortraitFocusCentral,Mathf.Abs(focus.z));
                float actionDistance=distance;
                // Fitting only the crossbar leaves the near post's ground
                // contact at the bottom edge during corner deliveries.
                for(int side=-1;side<=1;side+=2){Fit(new Vector3(direction*52.5f,2.44f,side*3.66f));Fit(new Vector3(direction*52.5f,0,side*3.66f));}
                // Ball and passing lanes remain safe. Only the extra distance for the
                // goal fades in: crossing x=28 used to move the camera by up to 26 m.
                distance=Mathf.Lerp(actionDistance,distance,blend);
            }
            if(wide){Fit(new Vector3(-52.5f,0,-34));Fit(new Vector3(-52.5f,0,34));Fit(new Vector3(52.5f,0,-34));Fit(new Vector3(52.5f,0,34));}
            camera.transform.SetPositionAndRotation(focus-forward*distance,rotation);
            camera.farClipPlane=Mathf.Max(270,distance+150);
            void Fit(Vector3 point){var relative=point-focus;float depth=Vector3.Dot(relative,forward);distance=Mathf.Max(distance,Mathf.Abs(Vector3.Dot(relative,right))/horizontal-depth,Mathf.Abs(Vector3.Dot(relative,up))/vertical-depth);}
        }
        static float Ramp(float from,float to,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));
    }
}
