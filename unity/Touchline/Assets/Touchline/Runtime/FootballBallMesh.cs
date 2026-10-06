using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    public static class FootballBallMesh
    {
        static Mesh shared;
        public static GameObject Create(Material light,Material dark)
        {
            var go=new GameObject("Ball · diameter 22 cm");go.AddComponent<MeshFilter>().sharedMesh=Shared;
            go.AddComponent<MeshRenderer>().sharedMaterials=new[]{light,dark};go.transform.localScale=Vector3.one*.22f;return go;
        }
        public static Mesh Shared
        {
            get{
                if(shared!=null)return shared;
                float p=(1+Mathf.Sqrt(5))/2;var ico=new List<Vector3>();
                for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2){ico.Add(new Vector3(a,b*p,0));ico.Add(new Vector3(0,a,b*p));ico.Add(new Vector3(b*p,0,a));}
                var faces=new List<int[]>();var neighbours=new HashSet<int>[12];for(int i=0;i<12;i++)neighbours[i]=new HashSet<int>();
                // Convex triangular faces of the icosahedron. Truncating each
                // edge produces twenty hexagons and twelve pentagons.
                for(int a=0;a<12;a++)for(int b=a+1;b<12;b++)for(int c=b+1;c<12;c++){
                    var n=Vector3.Cross(ico[b]-ico[a],ico[c]-ico[a]);bool positive=false,negative=false;
                    for(int i=0;i<12;i++){float d=Vector3.Dot(n,ico[i]-ico[a]);positive|=d>.001f;negative|=d<-.001f;}
                    if(positive&&negative)continue;
                    var face=Vector3.Dot(n,ico[a])>0?new[]{a,b,c}:new[]{a,c,b};faces.Add(face);
                    for(int i=0;i<3;i++){neighbours[face[i]].Add(face[(i+1)%3]);neighbours[face[(i+1)%3]].Add(face[i]);}
                }
                var vertices=new List<Vector3>();var white=new List<int>();var black=new List<int>();
                foreach(var f in faces){var polygon=new List<Vector3>();for(int i=0;i<3;i++){var a=ico[f[i]];var b=ico[f[(i+1)%3]];polygon.Add((a*2+b).normalized*.5f);polygon.Add((a+b*2).normalized*.5f);}Panel(polygon,vertices,white);}
                for(int i=0;i<12;i++){
                    var polygon=new List<Vector3>();foreach(int other in neighbours[i])polygon.Add((ico[i]*2+ico[other]).normalized*.5f);
                    var normal=ico[i].normalized;var tangent=Vector3.ProjectOnPlane(polygon[0],normal).normalized;var bitangent=Vector3.Cross(normal,tangent);
                    polygon.Sort((a,b)=>Mathf.Atan2(Vector3.Dot(a,bitangent),Vector3.Dot(a,tangent)).CompareTo(Mathf.Atan2(Vector3.Dot(b,bitangent),Vector3.Dot(b,tangent))));Panel(polygon,vertices,black);
                }
                var normals=new List<Vector3>(vertices.Count);foreach(var v in vertices)normals.Add(v.normalized);
                shared=new Mesh{name="Rounded 32-panel football"};shared.SetVertices(vertices);shared.SetNormals(normals);shared.subMeshCount=2;shared.SetTriangles(white,0);shared.SetTriangles(black,1);shared.RecalculateBounds();return shared;
            }
        }
        static void Panel(List<Vector3> polygon,List<Vector3> vertices,List<int> indices)
        {
            var centre=Vector3.zero;foreach(var p in polygon)centre+=p;centre=centre.normalized*.5f;
            if(Vector3.Dot(Vector3.Cross(polygon[1]-polygon[0],polygon[2]-polygon[0]),centre)<0)polygon.Reverse();
            for(int i=0;i<polygon.Count;i++){
                var a=centre;var b=polygon[i];var c=polygon[(i+1)%polygon.Count];var ab=(a+b).normalized*.5f;var bc=(b+c).normalized*.5f;var ca=(c+a).normalized*.5f;
                Triangle(a,ab,ca,vertices,indices);Triangle(ab,b,bc,vertices,indices);Triangle(ca,bc,c,vertices,indices);Triangle(ab,bc,ca,vertices,indices);
            }
        }
        static void Triangle(Vector3 a,Vector3 b,Vector3 c,List<Vector3> vertices,List<int> indices){int first=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(first);indices.Add(first+1);indices.Add(first+2);}
    }
    public sealed class BallVisualRotation
    {
        Vector3 previous,spin;bool initialized,wasAirborne;
        public Quaternion Rotation{get;private set;}=Quaternion.identity;
        public void Advance(Vector3 position,bool held,float seconds)
        {
            if(!initialized){previous=position;initialized=true;return;}
            if(seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return;
            var delta=Vector3.ProjectOnPlane(position-previous,Vector3.up);previous=position;
            if(held||delta.magnitude>8){spin=Vector3.zero;wasAirborne=false;return;}
            bool airborne=position.y>.24f;
            if(!airborne){if(delta.sqrMagnitude>.0000001f){var axis=Vector3.Cross(Vector3.up,delta).normalized;float angle=delta.magnitude/.11f*Mathf.Rad2Deg;Rotation=Quaternion.AngleAxis(angle,axis)*Rotation;spin=axis*(angle/seconds);}}
            else{
                if(!wasAirborne&&delta.sqrMagnitude>.0000001f)spin=Vector3.Cross(Vector3.up,delta).normalized*Mathf.Clamp(delta.magnitude/seconds/.11f*Mathf.Rad2Deg*.35f,180,1800);
                if(spin.sqrMagnitude>.01f)Rotation=Quaternion.AngleAxis(spin.magnitude*seconds,spin.normalized)*Rotation;
            }
            wasAirborne=airborne;
        }
    }
}
