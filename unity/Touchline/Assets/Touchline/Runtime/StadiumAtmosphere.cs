using System;
using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    // Seated silhouettes share one mesh and seven materials. No spectator objects,
    // transparency, per-frame animation or shadow passes compete with the football.
    public static class StadiumAtmosphere
    {
        public const int MaterialCount=7;
        public static Color[] Palette(Color home,Color away)
        {
            home.a=away.a=1;
            return new[]{Color.Lerp(home,new Color(.08f,.09f,.12f),.28f),Color.Lerp(home,new Color(.62f,.65f,.66f),.6f),
                new Color(.12f,.15f,.19f),new Color(.31f,.34f,.36f),Color.Lerp(away,new Color(.12f,.14f,.17f),.35f),
                new Color(.58f,.40f,.29f),new Color(.28f,.19f,.15f)};
        }
        public static Mesh Crowd(string home,string away,float occupancy=.84f)
        {
            occupancy=float.IsNaN(occupancy)||float.IsInfinity(occupancy)?0:Mathf.Clamp01(occupancy);
            uint state=2166136261;foreach(char c in (home??"")+"|"+(away??"")){state^=c;state*=16777619;}
            var builder=new CrowdBuilder();
            uint Next(){state=state*1664525+1013904223;return state;}
            void Add(Vector3 position,Vector3 towardPitch,bool visiting,bool detailed)
            {
                uint sample=Next();if((sample&65535)/65536f>=occupancy)return;
                float height=.44f+((sample>>16)&15)*.012f;
                int clothing=visiting&&sample%4==0?4:(int)((sample>>21)%4);
                builder.Person(position,towardPitch,height,clothing,5+(int)((sample>>25)&1),sample,detailed);
            }
            for(int side=-1;side<=1;side+=2){
                for(int row=0;row<9;row++)for(int block=0;block<6;block++)for(int seat=0;seat<14;seat++){
                    float x=-47+block*18.8f-7.8f+seat*1.2f;
                    Add(new Vector3(x,row*.65f+.61f,side*(39+row*1.1f)),new Vector3(0,0,-side),false,row<2);
                }
                for(int row=0;row<10;row++)for(int block=0;block<4;block++)for(int seat=0;seat<15;seat++){
                    float z=-31.5f+block*21-8.4f+seat*1.2f;
                    Add(new Vector3(side*(60+row*1.15f),row*.7f+.39f,z),new Vector3(-side,0,0),side==1&&block==3,row<2);
                }
            }
            return builder.Build();
        }
        sealed class CrowdBuilder
        {
            static readonly int[] TorsoIndices={0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0,4,7,6,4,6,5};
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Vector3> cardNormals=new List<Vector3>();
            readonly List<int>[] triangles=new List<int>[MaterialCount];
            public CrowdBuilder(){for(int i=0;i<MaterialCount;i++)triangles[i]=new List<int>();}
            int Vertex(Vector3 point,Vector3 cardNormal=default){int index=vertices.Count;vertices.Add(point);cardNormals.Add(cardNormal);return index;}
            void Triangle(int material,int a,int b,int c){triangles[material].Add(a);triangles[material].Add(b);triangles[material].Add(c);}
            void Quad(int material,int a,int b,int c,int d){Triangle(material,a,b,c);Triangle(material,a,c,d);}
            void Card(int material,Vector3 normal,params Vector3[] points){
                int start=vertices.Count;foreach(var point in points)Vertex(point,normal);
                // Two opaque faces share vertices. Explicit normals prevent
                // opposite triangle windings from cancelling the lighting.
                for(int i=1;i<points.Length-1;i++){Triangle(material,start,start+i,start+i+1);Triangle(material,start,start+i+1,start+i);}
            }
            public void Person(Vector3 seat,Vector3 facing,float height,int clothing,int skin,uint variation,bool detailed)
            {
                var right=Vector3.Cross(Vector3.up,facing);float shoulder=.19f+((variation>>18)&3)*.011f;
                float lean=.025f+((variation>>27)&3)*.009f;var top=seat+Vector3.up*height+facing*lean;
                Vector3 At(float x,float y,float z)=>seat+right*x+Vector3.up*y+facing*z;
                var normal=(facing+Vector3.up*.12f).normalized;
                if(!detailed){
                    Card(clothing,normal,At(-.135f,0,0),At(.135f,0,0),top+right*shoulder,top-right*shoulder);
                    var head=top+Vector3.up*.115f;float width=.091f+((variation>>23)&1)*.009f;
                    Card(skin,normal,head-right*width,head-right*(width*.52f)+Vector3.up*.105f,head+right*(width*.52f)+Vector3.up*.105f,head+right*width,head+right*(width*.52f)-Vector3.up*.115f,head-right*(width*.52f)-Vector3.up*.115f);
                    return;
                }
                int b=vertices.Count;var depth=facing*.12f;
                Vertex(seat-right*.135f-depth);Vertex(seat+right*.135f-depth);Vertex(seat+right*.135f+depth);Vertex(seat-right*.135f+depth);
                Vertex(top-right*shoulder-depth);Vertex(top+right*shoulder-depth);Vertex(top+right*shoulder+depth);Vertex(top-right*shoulder+depth);
                foreach(var f in TorsoIndices)triangles[clothing].Add(b+f);
                // Three bevelled rings give the head a small flat crown and
                // a jaw, instead of the previous tall octahedral point.
                int h=vertices.Count;var center=top+Vector3.up*.12f;
                for(int ring=0;ring<3;ring++){
                    float y=ring==0?-.115f:ring==1?0:.11f,w=ring==1?.095f:.056f,d=ring==1?.085f:.055f;
                    Vertex(center+Vector3.up*y-right*w-facing*d);Vertex(center+Vector3.up*y+right*w-facing*d);Vertex(center+Vector3.up*y+right*w+facing*d);Vertex(center+Vector3.up*y-right*w+facing*d);
                }
                bool hair=((variation>>29)&3)!=0;
                for(int ring=0;ring<2;ring++)for(int side=0;side<4;side++){int a=h+ring*4+side,n=h+ring*4+(side+1)%4;Quad(ring==1&&hair?2:skin,a,a+4,n+4,n);}
                Quad(hair?2:skin,h+8,h+11,h+10,h+9);Quad(skin,h,h+1,h+2,h+3);
                // Resting arms and projecting thighs define a seated posture.
                // At this distance opaque strips avoid extra spectator rigs.
                float elbow=.285f+((variation>>20)&1)*.02f,handDepth=.23f+((variation>>22)&1)*.025f;
                for(int side=-1;side<=1;side+=2){
                    Card(((variation>>26)&1)==0?clothing:skin,normal,At(side*shoulder,height-.035f,lean+.035f),At(side*elbow,height*.37f,.14f),At(side*.14f,.065f,handDepth),At(side*(elbow-.055f),height*.38f,.15f));
                    float x=side*.09f;Card(2,normal,At(x-.043f,.005f,.08f),At(x+.043f,.005f,.08f),At(x+.043f,-.065f,.31f),At(x-.043f,-.065f,.31f));
                }
            }
            public Mesh Build(){var mesh=new Mesh{name="Batched seated supporters",subMeshCount=MaterialCount};mesh.SetVertices(vertices);for(int i=0;i<MaterialCount;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();var normals=mesh.normals;for(int i=0;i<normals.Length;i++)if(cardNormals[i].sqrMagnitude>.1f)normals[i]=cardNormals[i];mesh.normals=normals;mesh.RecalculateBounds();return mesh;}
        }
    }
}
