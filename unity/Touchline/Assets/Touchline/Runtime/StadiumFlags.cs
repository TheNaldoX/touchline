using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    // Drapeaux qui flottent : quelques grands drapeaux bicolores agités dans le virage
    // populaire (plus fort après un but du club) et les quatre drapeaux de coin.
    // Un seul maillage, trois sous-maillages qui réutilisent les matières des tribunes
    // (couleur du club, couleur claire du club, hampes sombres). Sommets mis à jour
    // à fréquence réduite (TickRate), ondulation sinusoïdale déterministe.
    public sealed class StadiumFlags
    {
        public const int SubmeshCount=3;                    // 0 club, 1 club clair, 2 hampes (= matières 0, 1, 2 des tribunes)
        public const int HomeFlags=7;                       // grands drapeaux du virage
        const int Columns=6;                                // segments le long d'un drapeau
        public const float FlagLength=1.7f,FlagHeight=1.1f; // m
        public const float PoleHeight=2.6f,PoleGrip=.6f;    // m : hampe, hauteur des mains au-dessus du siège
        const float PoleWidth=.04f;                         // m
        public const float CornerLength=.45f,CornerHeight=.3f,CornerPost=1.5f; // m (drapeau de coin)
        public const float TickRate=15f;                    // mises à jour par seconde
        public const float CalmWave=.1f,CheerWave=.38f;     // m d'amplitude au bout libre (calme / après un but)
        const float CalmRate=.8f,CheerRate=2.3f;            // ondulations par seconde
        const float CornerWave=.06f,CornerRate=1.1f;        // m, ondulations par seconde (vent léger)
        const float WaveLength=1.6f;                        // m, longueur d'onde le long du tissu
        const float GoalLineX=52.5f,TouchLineZ=34f;         // m

        struct Cloth{public int first;public Vector3 normal;public bool corner;public float phase;}
        readonly Mesh mesh;readonly Vector3[] rest,current;readonly float[] along;readonly List<Cloth> cloths=new List<Cloth>();
        float time,lastTick=float.NegativeInfinity,homePhase,cornerPhase;
        public Mesh Mesh=>mesh;
        public float HomeAmplitude {get;private set;}=CalmWave;

        public StadiumFlags()
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var distance=new List<float>();
            var triangles=new List<int>[SubmeshCount];for(int i=0;i<SubmeshCount;i++)triangles[i]=new List<int>();
            void Flag(Vector3 top,Vector3 direction,Vector3 normal,float length,float height,int upper,int lower,bool corner,float phase)
            {
                int first=vertices.Count;
                for(int row=0;row<3;row++)for(int column=0;column<=Columns;column++){
                    float u=length*column/Columns;vertices.Add(top+direction*u-Vector3.up*(height*row*.5f));normals.Add(normal);distance.Add(u/length);
                }
                for(int row=0;row<2;row++)for(int column=0;column<Columns;column++){
                    int a=first+row*(Columns+1)+column,b=a+1,c=a+Columns+2,d=a+Columns+1;var list=triangles[row==0?upper:lower];
                    // Deux faces sur les mêmes sommets (tissu vu des deux côtés).
                    list.AddRange(new[]{a,b,c,a,c,d,a,c,b,a,d,c});
                }
                cloths.Add(new Cloth{first=first,normal=normal,corner=corner,phase=phase});
            }
            void Pole(Vector3 bottom,float height)
            {
                int first=vertices.Count;var h=PoleWidth*.5f;
                for(int level=0;level<2;level++)for(int k=0;k<4;k++){
                    var offset=new Vector3(k==1||k==2?h:-h,level*height,k>=2?h:-h);vertices.Add(bottom+offset);normals.Add(new Vector3(offset.x,0,offset.z).normalized);distance.Add(0);
                }
                for(int k=0;k<4;k++){int a=first+k,b=first+(k+1)%4;triangles[2].AddRange(new[]{a,b+4,b,a,a+4,b+4});}
            }
            // Virage populaire (x < 0) : drapeaux tenus à bout de bras dans les derniers rangs.
            int side=StadiumAtmosphere.HomeEndSide;
            for(int i=0;i<HomeFlags;i++){
                int row=5+(i*3)%4;float z=-33+i*11f;                                    // rangs 5 à 8, répartis sur la largeur
                var seat=new Vector3(side*(60+row*1.15f),row*.7f+.39f,z);               // même siège que StadiumAtmosphere.Crowd
                var bottom=seat+Vector3.up*PoleGrip;Pole(bottom,PoleHeight);
                Flag(bottom+Vector3.up*PoleHeight,Vector3.forward,new Vector3(-side,0,0),FlagLength,FlagHeight,0,1,false,i*1.7f);
            }
            for(int end=-1;end<=1;end+=2)for(int touch=-1;touch<=1;touch+=2)
                Flag(new Vector3(end*GoalLineX,CornerPost,touch*TouchLineZ),Vector3.right,new Vector3(0,0,touch),CornerLength,CornerHeight,0,0,true,end+touch*.5f);
            mesh=new Mesh{name="Waving flags",subMeshCount=SubmeshCount};mesh.SetVertices(vertices);mesh.SetNormals(normals);
            for(int i=0;i<SubmeshCount;i++)mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(2*CheerWave);mesh.bounds=bounds;mesh.MarkDynamic();
            rest=vertices.ToArray();current=(Vector3[])rest.Clone();along=distance.ToArray();
        }

        // excitement : 0 calme, 1 juste après un but du club recevant (CrowdReaction.Excitement).
        public void Advance(float dt,float excitement)
        {
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            excitement=Mathf.Clamp01(excitement);time+=dt;
            homePhase+=2*Mathf.PI*Mathf.Lerp(CalmRate,CheerRate,excitement)*dt;cornerPhase+=2*Mathf.PI*CornerRate*dt;
            HomeAmplitude=Mathf.Lerp(CalmWave,CheerWave,excitement);
            if(time-lastTick<1/TickRate)return;lastTick=time;
            int count=3*(Columns+1);
            foreach(var cloth in cloths){
                float amplitude=cloth.corner?CornerWave:HomeAmplitude,phase=(cloth.corner?cornerPhase:homePhase)+cloth.phase;
                float length=cloth.corner?CornerLength:FlagLength;
                for(int v=cloth.first;v<cloth.first+count;v++){
                    float u=along[v];current[v]=rest[v]+cloth.normal*(amplitude*u*Mathf.Sin(phase-2*Mathf.PI*u*length/WaveLength));
                }
            }
            mesh.SetVertices(current);
        }
        public Vector3[] Vertices=>current;
    }
}
