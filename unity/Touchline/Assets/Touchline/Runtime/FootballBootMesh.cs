using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    public static class FootballBootMesh
    {
        static Mesh shared;
        public static Mesh Shared
        {
            get{
                if(shared!=null)return shared;
                // Rounded sections: heel/collar, instep, toe and toe cap.
                // The collar overlaps the sock around the ankle joint.
                float[] z={-.07f,.025f,.15f,.22f},width={.044f,.057f,.056f,.025f},top={.105f,.075f,.012f,-.004f};
                var vertices=new Vector3[34];var triangles=new List<int>(192);
                for(int section=0;section<4;section++)for(int edge=0;edge<8;edge++){
                    float angle=edge*Mathf.PI/4;float middle=(top[section]-.065f)*.5f,half=(top[section]+.065f)*.5f;
                    vertices[section*8+edge]=new Vector3(Mathf.Cos(angle)*width[section],middle+Mathf.Sin(angle)*half,z[section]);
                    if(section<3){int a=section*8+edge,b=section*8+(edge+1)%8,c=a+8,d=b+8;triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(b);triangles.Add(d);triangles.Add(c);}
                }
                vertices[32]=new Vector3(0,.02f,z[0]);vertices[33]=new Vector3(0,-.03f,z[3]);
                for(int edge=0;edge<8;edge++){triangles.Add(32);triangles.Add((edge+1)%8);triangles.Add(edge);triangles.Add(33);triangles.Add(24+edge);triangles.Add(24+(edge+1)%8);}
                shared=new Mesh{name="Football boot with ankle collar"};shared.vertices=vertices;shared.SetTriangles(triangles,0);shared.RecalculateNormals();shared.RecalculateBounds();return shared;
            }
        }
    }
}
