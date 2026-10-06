using System;
using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    // Seated silhouettes share one mesh and seven materials. No spectator objects,
    // transparency, per-frame animation or shadow passes compete with the football.
    public static class StadiumAtmosphereBaseline
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
            void Add(Vector3 position,Vector3 towardPitch,bool visiting)
            {
                uint sample=Next();if((sample&65535)/65536f>=occupancy)return;
                float height=.44f+((sample>>16)&15)*.012f;
                int clothing=visiting&&sample%4==0?4:(int)((sample>>21)%4);
                builder.Person(position,towardPitch,height,clothing,5+(int)((sample>>25)&1));
            }
            for(int side=-1;side<=1;side+=2){
                for(int row=0;row<9;row++)for(int block=0;block<6;block++)for(int seat=0;seat<14;seat++){
                    float x=-47+block*18.8f-7.8f+seat*1.2f;
                    Add(new Vector3(x,row*.65f+.61f,side*(39+row*1.1f)),new Vector3(0,0,-side),false);
                }
                for(int row=0;row<10;row++)for(int block=0;block<4;block++)for(int seat=0;seat<15;seat++){
                    float z=-31.5f+block*21-8.4f+seat*1.2f;
                    Add(new Vector3(side*(60+row*1.15f),row*.7f+.39f,z),new Vector3(-side,0,0),side==1&&block==3);
                }
            }
            return builder.Build();
        }
        sealed class CrowdBuilder
        {
            static readonly int[] TorsoIndices={0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0,4,7,6,4,6,5};
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<int>[] triangles=new List<int>[MaterialCount];
            public CrowdBuilder(){for(int i=0;i<MaterialCount;i++)triangles[i]=new List<int>();}
            void Triangle(int material,int a,int b,int c){triangles[material].Add(a);triangles[material].Add(b);triangles[material].Add(c);}
            public void Person(Vector3 foot,Vector3 facing,float height,int clothing,int skin)
            {
                int b=vertices.Count;var right=Vector3.Cross(Vector3.up,facing);var depth=facing*.13f;
                var top=foot+Vector3.up*height;
                // Tapered torso, with a slight forward lean, rather than square Lego blocks.
                vertices.Add(foot-right*.13f-depth);vertices.Add(foot+right*.13f-depth);vertices.Add(foot+right*.13f+depth);vertices.Add(foot-right*.13f+depth);
                top+=facing*.035f;vertices.Add(top-right*.22f-depth);vertices.Add(top+right*.22f-depth);vertices.Add(top+right*.22f+depth);vertices.Add(top-right*.22f+depth);
                foreach(var f in TorsoIndices)triangles[clothing].Add(b+f);
                int h=vertices.Count;var head=top+Vector3.up*.14f;
                vertices.Add(head+Vector3.up*.16f);vertices.Add(head-Vector3.up*.13f);vertices.Add(head+right*.105f);vertices.Add(head-right*.105f);vertices.Add(head+facing*.11f);vertices.Add(head-facing*.11f);
                Triangle(skin,h,h+4,h+2);Triangle(skin,h,h+3,h+4);Triangle(skin,h,h+5,h+3);Triangle(skin,h,h+2,h+5);
                Triangle(skin,h+1,h+2,h+4);Triangle(skin,h+1,h+4,h+3);Triangle(skin,h+1,h+3,h+5);Triangle(skin,h+1,h+5,h+2);
            }
            public Mesh Build(){var mesh=new Mesh{name="Batched seated supporters",subMeshCount=MaterialCount};mesh.SetVertices(vertices);for(int i=0;i<MaterialCount;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }
    }
}
