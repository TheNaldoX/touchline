using UnityEngine;

namespace Touchline
{
    // Filet qui réagit au but : la zone touchée par le ballon se creuse vers
    // l'extérieur selon sa vitesse, puis revient en oscillant (ressort amorti).
    // Les sommets ne sont recalculés que pendant le mouvement : coût nul au repos.
    public sealed class GoalNetRipple
    {
        const float Radius=1.1f;         // m, zone du filet entraînée autour du ballon
        const float MaxBulge=.30f;       // m, creux maximal
        const float SpeedToBulge=.022f;  // m de creux par m/s de vitesse du ballon dans le filet
        const float Stiffness=70f;       // 1/s², raideur du retour
        const float Damping=6f;          // 1/s, amortissement (quelques oscillations visibles)
        const float MaxStep=.05f;        // s, pas d'intégration maximal (stabilité)
        const float RestBulge=.0005f,RestSpeed=.005f; // m, m/s : en dessous, filet considéré au repos
        public const float GoalLine=52.5f,HalfWidth=3.66f,Crossbar=2.44f; // m

        readonly Mesh mesh;readonly Vector3[] rest,current;readonly int side;readonly Vector3 mouth;
        float bulge,bulgeVelocity;Vector3 centre,lastBall;bool tracking,moving;
        public float Bulge=>bulge;

        public GoalNetRipple(Mesh net,int side)
        {
            mesh=net;this.side=side>=0?1:-1;rest=net.vertices;current=(Vector3[])rest.Clone();mouth=new Vector3(this.side*GoalLine,Crossbar*.5f,0);
            net.MarkDynamic();var bounds=net.bounds;bounds.Expand(MaxBulge*2);net.bounds=bounds;
        }
        public static bool InNet(Vector3 ball,int side)=>ball.x*side>GoalLine&&Mathf.Abs(ball.z)<HalfWidth+.3f&&ball.y<Crossbar+.3f;

        public void Advance(Vector3 ball,bool goalShown,float dt)
        {
            if(dt<=0)return;
            bool inside=goalShown&&InNet(ball,side);float drive=0;
            if(inside){
                if(tracking){float speed=(ball-lastBall).magnitude/dt;drive=Mathf.Min(MaxBulge,speed*SpeedToBulge);}
                centre=ball;moving=true;
            }
            tracking=inside;lastBall=ball;
            if(!moving)return;
            // Ressort amorti vers le creux imposé par le ballon (0 quand il est arrêté).
            for(float left=dt;left>0;left-=MaxStep){float step=Mathf.Min(MaxStep,left);
                bulgeVelocity+=(Stiffness*(drive-bulge)-Damping*bulgeVelocity)*step;bulge+=bulgeVelocity*step;}
            if(!inside&&Mathf.Abs(bulge)<RestBulge&&Mathf.Abs(bulgeVelocity)<RestSpeed){bulge=bulgeVelocity=0;moving=false;}
            for(int i=0;i<rest.Length;i++){
                var p=rest[i];float d=(p-centre).magnitude;if(d>=Radius){current[i]=p;continue;}
                float w=1-d/Radius;w*=w;var outward=(p-mouth).normalized;current[i]=p+outward*(bulge*w);
            }
            mesh.SetVertices(current);
        }
    }
}
