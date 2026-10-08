using UnityEngine;

namespace Touchline
{
    // Caméra du ralenti de but : plan bas à côté du but marqué, qui suit le ballon
    // (et donc le buteur) avec un regard amorti, avance lentement et ouvre son champ
    // à mesure que l'action approche. Aucun saut : position et regard sont interpolés.
    // Apply ne fait qu'appliquer l'état (rappelable sans effet de bord, ex. pliage).
    public sealed class GoalReplayCamera
    {
        public const float GoalLine=52.5f;          // m
        public const float BehindLine=2.8f;         // m derrière la ligne de but au départ (filet jusqu'à 1,8 m, panneaux à 3,5 m)
        public const float PushIn=1f;               // m d'avancée lente vers le terrain pendant le ralenti
        public const float PushDuration=6f;         // s pour cette avancée
        public const float BesidePost=10f;          // m du centre du but, côté caméra principale (filet et poteau pas trop près)
        public const float Height=2f;               // m, plan bas (un peu au-dessus des joueurs)
        public const float LookAbove=.6f;           // m au-dessus du ballon : corps du buteur dans le cadre
        public const float FrameWidth=13f;          // m de scène gardés autour du ballon
        public const float MinFov=14f,MaxFov=50f;   // ° de champ vertical (paysage)
        public const float PortraitMaxFov=80f;      // ° de champ vertical au plus sur écran plié (portrait)
        public const float LookSmoothing=.2f;       // s, amorti du regard pour une action lointaine
        const float SmoothingDistance=30f;          // m : en deçà, l'amorti raccourcit (le ballon traverse vite l'image de près)
        const float MinSmoothingShare=.25f;         // part minimale de LookSmoothing tout près du but
        const float MaxLookSpeed=60f;               // m/s

        int side=1;Vector3 look,lookVelocity;float elapsed;bool snap=true;
        public int Side=>side;
        public Vector3 Look=>look;
        // goalX : abscisse du ballon au moment du but (côté du but marqué).
        public void Begin(float goalX,Vector3 ball){side=goalX>=0?1:-1;elapsed=0;snap=true;look=ball+Vector3.up*LookAbove;lookVelocity=Vector3.zero;}
        public Vector3 Position=>new Vector3(side*(GoalLine+BehindLine-PushIn*Mathf.SmoothStep(0,1,elapsed/PushDuration)),Height,BesidePost);
        public void Advance(Vector3 ball,float dt)
        {
            var target=ball+Vector3.up*LookAbove;
            if(snap){look=target;lookVelocity=Vector3.zero;snap=false;}
            else if(dt>0){float near=Mathf.Clamp((target-Position).magnitude/SmoothingDistance,MinSmoothingShare,1);
                look=Vector3.SmoothDamp(look,target,ref lookVelocity,LookSmoothing*near,MaxLookSpeed,dt);}
            if(dt>0)elapsed+=dt;
        }
        // Champ vertical (°) qui garde FrameWidth autour du regard, selon le format de l'écran.
        public static float FieldOfView(float distance,float aspect)
        {
            float half=Mathf.Atan(FrameWidth*.5f/Mathf.Max(.5f,distance));
            float vertical=2*Mathf.Atan(Mathf.Tan(half)/Mathf.Max(.2f,aspect))*Mathf.Rad2Deg;
            // Écran étroit : le champ vertical peut s'ouvrir davantage pour garder la largeur utile.
            return Mathf.Clamp(vertical,MinFov,Mathf.Min(PortraitMaxFov,MaxFov/Mathf.Clamp(aspect,.2f,1)));
        }
        public void Apply(Camera camera)
        {
            var position=Position;var direction=look-position;if(direction.sqrMagnitude<.01f)direction=new Vector3(-side,0,0);
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(direction,Vector3.up));
            camera.fieldOfView=FieldOfView(direction.magnitude,camera.aspect);
        }
    }
}
