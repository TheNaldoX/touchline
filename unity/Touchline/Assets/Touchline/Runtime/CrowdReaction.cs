using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    // Découpage du maillage groupé des tribunes : premier sommet de chaque supporter,
    // son siège, son camp (bloc visiteur ou non) et la levée de chaque sommet quand
    // les bras ou l'écharpe montent (m). Aucun objet par spectateur.
    public sealed class CrowdRig
    {
        public readonly int[] personStart;public readonly Vector3[] seat;public readonly bool[] visiting;public readonly float[] raise;
        public CrowdRig(int[] personStart,Vector3[] seat,bool[] visiting,float[] raise){this.personStart=personStart;this.seat=seat;this.visiting=visiting;this.raise=raise;}
        public int People=>personStart.Length;
        public int End(int person)=>person+1<personStart.Length?personStart[person+1]:raise.Length;
    }

    // Les tribunes réagissent : sur un but, les supporters du camp qui marque se lèvent
    // en vague depuis le but visé, sautent, lèvent bras et écharpes, puis se rassoient ;
    // sur une frappe, ils se soulèvent à moitié. Les sommets ne sont recalculés que
    // pendant une réaction, à fréquence réduite (TickRate), et seulement pour le camp
    // concerné : coût nul au repos. Déterministe : participation et phase tirées de
    // l'indice du supporter, temps de présentation cumulé.
    public sealed class CrowdReaction
    {
        public enum Kind{None,Chance,Goal}
        public const float TickRate=20f;            // mises à jour du maillage par seconde pendant une réaction
        public const float StandLift=.3f;           // m gagnés en se levant
        public const float ChanceLift=.6f;          // part de StandLift sur une occasion (à moitié levés)
        public const float ChanceArms=.45f;         // part de la levée des bras sur une occasion (mains sur la tête)
        public const float JumpHeight=.14f;         // m, saut de célébration
        public const float JumpRate=2.2f;           // sauts par seconde
        public const float WaveSpeed=32f;           // m/s, propagation de la vague depuis le but
        public const float RiseTime=.35f,SitTime=1.2f;      // s
        public const float GoalHold=5.5f,ChanceHold=1.1f;   // s debout
        public const float GoalShare=.9f,ChanceShare=.55f;  // part des supporters du camp qui réagissent
        public const float JumperShare=.6f;         // part des célébrants qui sautent
        const float MaxWaveDelay=4.5f;              // s, vague la plus lointaine (≈ 140 m)
        const float FlagExcitementDecay=.6f;        // 1/s, retour au calme des drapeaux
        const float GoalLineX=52.5f;                // m

        readonly Mesh mesh;readonly CrowdRig rig;readonly Vector3[] rest,current;
        readonly Kind[] kind=new Kind[2];readonly float[] start=new float[2];readonly Vector3[] origin=new Vector3[2];readonly bool[] dirty=new bool[2];
        readonly int[] knownScore=new int[2],knownShots=new int[2];
        float time,lastTick=float.NegativeInfinity;bool observed;
        public float PresentationTime=>time; // s de présentation cumulées (déterministe)
        public Kind Current(int team)=>kind[team];

        public CrowdReaction(Mesh crowd,CrowdRig rig)
        {
            mesh=crowd;this.rig=rig;rest=crowd.vertices;current=(Vector3[])rest.Clone();
            crowd.MarkDynamic();var bounds=crowd.bounds;bounds.Expand(2*(StandLift+JumpHeight+StadiumAtmosphere.HandRaise));crowd.bounds=bounds;
        }
        // Camp d'un supporter : 0 = club recevant (State.home), 1 = bloc visiteur.
        public int Team(int person)=>rig.visiting[person]?1:0;

        // Compare score et tirs au dernier état vu ; react=false met seulement à jour la mémoire.
        public void Observe(MatchSimulation simulation,bool react=true)
        {
            var m=simulation.State;
            for(int side=0;side<2;side++){
                bool goal=m.score[side]>knownScore[side],shot=m.shots[side]>knownShots[side];
                knownScore[side]=m.score[side];knownShots[side]=m.shots[side];
                if(!observed||!react)continue;
                var target=new Vector3(simulation.Direction(side)*GoalLineX,0,0);
                if(goal)Trigger(side,Kind.Goal,target);else if(shot)Trigger(side,Kind.Chance,target);
            }
            observed=true;
        }
        public void Trigger(int team,Kind reaction,Vector3 goal)
        {
            if(reaction==Kind.None)return;
            // Une occasion n'interrompt pas un but en cours de célébration.
            if(reaction==Kind.Chance&&kind[team]==Kind.Goal&&time-start[team]<Duration(Kind.Goal))return;
            kind[team]=reaction;start[team]=time;origin[team]=goal;dirty[team]=true;lastTick=float.NegativeInfinity;
        }
        public static float Hold(Kind reaction)=>reaction==Kind.Goal?GoalHold:reaction==Kind.Chance?ChanceHold:0;
        public static float Duration(Kind reaction)=>reaction==Kind.None?0:RiseTime+Hold(reaction)+SitTime+MaxWaveDelay;
        // Levée normalisée (0 assis, 1 debout) t secondes après le passage de la vague.
        public static float Envelope(Kind reaction,float t)
        {
            if(reaction==Kind.None||t<=0)return 0;
            if(t<RiseTime)return Mathf.SmoothStep(0,1,t/RiseTime);t-=RiseTime;
            float hold=Hold(reaction);if(t<hold)return 1;t-=hold;
            return t<SitTime?Mathf.SmoothStep(1,0,t/SitTime):0;
        }
        static float Hash01(int person,uint salt){uint h=((uint)person+salt)*2654435761u;h^=h>>15;h*=2246822519u;h^=h>>13;return (h>>8)/16777216f;}
        // Hauteur gagnée par le supporter (m) et part de levée des bras (0–1).
        public static void Pose(Kind reaction,float sinceWave,int person,out float lift,out float arms)
        {
            lift=arms=0;if(reaction==Kind.None)return;
            bool goal=reaction==Kind.Goal;
            if(Hash01(person,17)>=(goal?GoalShare:ChanceShare))return;
            // Petit retard individuel (0–0,25 s) : la tribune ne se lève pas comme un seul homme.
            float t=sinceWave-.25f*Hash01(person,91),envelope=Envelope(reaction,t);if(envelope<=0)return;
            lift=envelope*StandLift*(goal?1:ChanceLift);arms=envelope*(goal?1:ChanceArms);
            if(goal&&Hash01(person,233)<JumperShare)lift+=envelope*JumpHeight*Mathf.Max(0,Mathf.Sin(2*Mathf.PI*(JumpRate*t+Hash01(person,4099))));
        }
        // Excitation du camp (0–1) pour les drapeaux : forte après un but, retombe doucement.
        public float Excitement(int team)
        {
            if(kind[team]==Kind.None)return 0;float since=time-start[team];
            float peak=kind[team]==Kind.Goal?1:.4f,calm=RiseTime+Hold(kind[team]);
            return since<calm?peak:Mathf.Max(0,peak*(1-(since-calm)*FlagExcitementDecay));
        }

        public void Advance(float dt)
        {
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            time+=dt;if(!dirty[0]&&!dirty[1])return;
            if(time-lastTick<1/TickRate)return;lastTick=time;
            for(int team=0;team<2;team++){
                if(!dirty[team])continue;
                bool finished=time-start[team]>=Duration(kind[team]);
                float nearest=float.MaxValue;if(!finished)for(int p=0;p<rig.People;p++)if(Team(p)==team)nearest=Mathf.Min(nearest,Vector3.Distance(rig.seat[p],origin[team]));
                for(int p=0;p<rig.People;p++){
                    if(Team(p)!=team)continue;int end=rig.End(p);
                    if(finished){for(int v=rig.personStart[p];v<end;v++)current[v]=rest[v];continue;}
                    float sinceWave=time-start[team]-(Vector3.Distance(rig.seat[p],origin[team])-nearest)/WaveSpeed;
                    Pose(kind[team],sinceWave,p,out float lift,out float arms);
                    for(int v=rig.personStart[p];v<end;v++){var r=rest[v];r.y+=lift+rig.raise[v]*arms;current[v]=r;}
                }
                if(finished){dirty[team]=false;kind[team]=Kind.None;}
            }
            mesh.SetVertices(current);
        }
    }
}
