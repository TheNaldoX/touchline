using UnityEngine;

namespace Touchline
{
    public static class BroadcastFraming
    {
        // Metres/second: zoom changes use wall-clock time, including at x10 playback.
        public static float SmoothDistance(float current,float target,ref float velocity,float dt)
            =>dt>0?Mathf.SmoothDamp(current,target,ref velocity,.9f,8f,dt):current;
        // Keep the ball and standing player inside a 12% screen margin while zoom catches up.
        public static Vector3 ConfineActionFocus(Camera camera,Vector3 focus,Vector3 ball,float distance)
        {
            var forward=camera.transform.forward;var right=camera.transform.right;var up=camera.transform.up;
            float vertical=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)*.76f; // same 12% margin as ConfineActionFocus
            float horizontal=vertical*Mathf.Max(.2f,camera.aspect);
            bool Fits(Vector3 center){return PointFits(ball-center)&&PointFits(ball+Vector3.up*2.2f-center);}
            bool PointFits(Vector3 relative){float depth=distance+Vector3.Dot(relative,forward);return depth>0&&Mathf.Abs(Vector3.Dot(relative,right))<=depth*horizontal&&Mathf.Abs(Vector3.Dot(relative,up))<=depth*vertical;}
            if(Fits(focus))return focus;
            var centered=new Vector3(ball.x,focus.y,ball.z);float low=0,high=1;
            for(int i=0;i<12;i++){float blend=(low+high)*.5f;if(Fits(Vector3.Lerp(focus,centered,blend)))high=blend;else low=blend;}
            return Vector3.Lerp(focus,centered,high);
        }
        // Fit real world points using the actual viewport aspect ratio.
        // Portrait, unfolded and ultra-wide layouts must retain the ball.
        // Pente de la caméra télé (descente par mètre d'avancée) : 0,58 ≈ 30°, proche des plans de diffusion.
        public const float BroadcastPitch=.58f;
        const float PortraitGoalApproachStart=8f; // m : ouverture plus étalée pour le champ horizontal étroit du téléphone
        const float GoalApproachStart=18f,GoalApproachFull=28f; // m depuis le milieu : ouverture progressive avant le dernier tiers
        const float GoalFocusStart=39f,GoalFocusFull=29f;       // m entre le regard et la ligne de but
        const float PortraitBallWide=24f,PortraitBallCentral=18f,PortraitFocusWide=18f,PortraitFocusCentral=12f; // m du centre en largeur
        // Portrait (Fold plié) : caméra inclinée dans l'axe du terrain (but +x en haut de l'écran), plus vue
        // du dessus. Les joueurs debout gagnent de la hauteur à l'écran ; les couloirs de passe dans la
        // longueur restent entiers, la largeur gardée autour du ballon est réduite (écran 2,3× plus haut que large).
        public const float PortraitTiltDegrees=45f;   // ° sous l'horizontale (90 = vue du dessus, ancienne vue)
        public const float PortraitLaneHalfWidth=5.5f;  // m : couloir proche lisible sur écran plié (paysage : 12)
        const float LaneHalfLength=18f,LaneHalfWidth=12f; // m : couloirs de passe gardés autour du regard (paysage)
        const float PortraitMinimumDistance=38f,MinimumDistance=46f,WideDistance=70f; // m à zoom 1
        public const float PortraitFocusFollow=.8f,PortraitFocusLimit=27f; // part de l'écart latéral du ballon suivie, borne (m)
        // Point regardé par la caméra télé : suit la longueur, n'accompagne qu'une partie de l'écart latéral.
        public static Vector3 LiveTarget(Vector3 ball,bool portrait,bool setPiece)
        {
            float lateral=portrait?Mathf.Clamp(ball.z*PortraitFocusFollow,-PortraitFocusLimit,PortraitFocusLimit):Mathf.Clamp(ball.z*(setPiece?.46f:.58f),-19,19);
            if(portrait){
                // Follow 35% of lateral ball displacement near goal, balancing ball and far post.
                float goalWeight=Ramp(PortraitGoalApproachStart,GoalApproachFull,Mathf.Abs(ball.x))*Ramp(PortraitBallWide,PortraitBallCentral,Mathf.Abs(ball.z));
                lateral=Mathf.Lerp(lateral,ball.z*.35f,goalWeight);
            }
            return new Vector3(Mathf.Clamp(ball.x*.90f,-46,46),.6f,lateral);
        }
        public static void Apply(Camera camera,Vector3 focus,Vector3 ball,bool wide,bool goal,int direction,float zoom)
        {
            bool portrait=camera.aspect<.8f;
            // Vue tactique portrait : terrain entier vu du dessus. Télé portrait : inclinée, même orientation à l'écran.
            float tilt=PortraitTiltDegrees*Mathf.Deg2Rad;
            var forward=portrait?(wide?new Vector3(0,-1,-.08f).normalized:new Vector3(Mathf.Cos(tilt),-Mathf.Sin(tilt),0)):new Vector3(-.035f,-BroadcastPitch,-1).normalized;
            var rotation=Quaternion.LookRotation(forward,portrait&&wide?Vector3.right:Vector3.up);
            var right=rotation*Vector3.right;var up=rotation*Vector3.up;
            float vertical=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)*.76f; // same 12% margin as ConfineActionFocus
            float horizontal=vertical*Mathf.Max(.2f,camera.aspect);
            zoom=Mathf.Clamp(zoom,.7f,1.4f);
            float distance=(wide?WideDistance:portrait?PortraitMinimumDistance:MinimumDistance)*zoom;
            if(!wide){
                // Keep a stable view of the passing lanes around the ball.
                // Portrait follows the length of the pitch, never a tiny landscape strip.
                float laneWidth=portrait?PortraitLaneHalfWidth:LaneHalfWidth;
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Fit(focus+new Vector3(x*LaneHalfLength*zoom,0,z*laneWidth*zoom));
            }
            Fit(ball);Fit(ball+Vector3.up*2.2f);
            // In portrait a corner and the whole goal cannot both fill a
            // readable frame. Follow the taker, then follow delivery centrally.
            if(goal&&!wide){
                float blend=Ramp(portrait?PortraitGoalApproachStart:GoalApproachStart,GoalApproachFull,Mathf.Abs(ball.x))*Ramp(GoalFocusStart,GoalFocusFull,Mathf.Abs(direction*52.5f-focus.x));
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
