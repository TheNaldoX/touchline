using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    // Each static bank/net is one surface, rather than dozens of renderers.
    // The net has physical thickness and remains visible from every camera.
    public static class StadiumGeometry
    {
        public static Mesh PitchMarkings()
        {
            var mesh=new Builder();
            mesh.GroundPath(new[]{new Vector3(-52.5f,0,-34),new Vector3(52.5f,0,-34),new Vector3(52.5f,0,34),new Vector3(-52.5f,0,34),new Vector3(-52.5f,0,-34)});
            mesh.GroundPath(new[]{new Vector3(0,0,-34),new Vector3(0,0,34)});mesh.Arc(Vector3.zero,9.15f,0,Mathf.PI*2,64);mesh.Spot(Vector3.zero);
            for(int side=-1;side<=1;side+=2){
                float goal=side*52.5f;var spot=new Vector3(side*41.5f,0,0);mesh.Spot(spot);
                mesh.GroundPath(new[]{new Vector3(goal,0,-20.16f),new Vector3(goal-side*16.5f,0,-20.16f),new Vector3(goal-side*16.5f,0,20.16f),new Vector3(goal,0,20.16f)});
                mesh.GroundPath(new[]{new Vector3(goal,0,-9.16f),new Vector3(goal-side*5.5f,0,-9.16f),new Vector3(goal-side*5.5f,0,9.16f),new Vector3(goal,0,9.16f)});
                float angle=Mathf.Acos(5.5f/9.15f),center=side>0?Mathf.PI:0;mesh.Arc(spot,9.15f,center-angle,center+angle,24);
                for(int edge=-1;edge<=1;edge+=2){float start=side>0?(edge>0?Mathf.PI:Mathf.PI*.5f):(edge>0?-Mathf.PI*.5f:0);mesh.Arc(new Vector3(goal,0,edge*34),1,start,start+Mathf.PI*.5f,12);}
            }
            return mesh.Build("Regulation pitch markings");
        }
        public static Mesh Stand(int side,bool seats)
        {
            var mesh=new Builder();
            for(int row=0;row<9;row++){
                if(!seats){mesh.Box(new Vector3(0,row*.65f,side*(39+row*1.1f)),new Vector3(119,.6f,1.2f));continue;}
                // Clear aisles break the continuous colour bars into believable seating blocks.
                for(int block=0;block<6;block++)mesh.Box(new Vector3(-47+block*18.8f,row*.65f+.45f,side*(39+row*1.1f)),new Vector3(17.1f,.35f,.68f));
            }
            return mesh.Build(seats?"Combined seats":"Combined terraces");
        }
        // Tribune d'en face (côté -Z, face à la caméra télé) : second anneau, toit et
        // mur du fond masquent le vide au-dessus des gradins. Côté caméra (+Z), rien
        // de haut : la caméra regarde par-dessus.
        public const float UpperTierFront=50f,UpperTierBase=8.6f;   // m : premier rang du second anneau (distance à l'axe, hauteur)
        public const float UpperRowDepth=1.1f,UpperRowRise=.7f;      // m par rang
        public const int UpperTierRows=8;
        public const float RoofHeight=17.5f,RoofFront=43.5f,StadiumBack=60f; // m
        public static Mesh UpperStand()
        {
            var mesh=new Builder();
            // Bandeau frontal (sous le premier rang du second anneau), puis rangées de sièges.
            mesh.Box(new Vector3(0,(UpperTierBase-2.2f+UpperTierBase)*.5f,-(UpperTierFront-.9f)),new Vector3(119,2.2f,.3f));
            for(int row=0;row<UpperTierRows;row++)
                mesh.Box(new Vector3(0,UpperTierBase+row*UpperRowRise-.3f,-(UpperTierFront+row*UpperRowDepth)),new Vector3(119,.6f,1.2f));
            return mesh.Build("Upper tier terraces");
        }
        public static Mesh StadiumShell()
        {
            var mesh=new Builder();
            // Toit en porte-à-faux au-dessus du second anneau, mur du fond, poteaux.
            mesh.Box(new Vector3(0,RoofHeight,-(RoofFront+StadiumBack)*.5f),new Vector3(124,.35f,StadiumBack-RoofFront));
            mesh.Box(new Vector3(0,RoofHeight*.5f,-StadiumBack),new Vector3(124,RoofHeight,.5f));
            for(int i=-3;i<=3;i++)mesh.Beam(new Vector3(i*19f,UpperTierBase+UpperTierRows*UpperRowRise,-(StadiumBack-.5f)),new Vector3(i*19f,RoofHeight,-(StadiumBack-.5f)),.35f);
            // Murs derrière les virages (au-dessus du dernier rang, hauteur 9 m).
            for(int side=-1;side<=1;side+=2)mesh.Box(new Vector3(side*72f,4.5f,0),new Vector3(.5f,9f,90));
            return mesh.Build("Stadium roof and back walls");
        }
        public static Mesh Surround(){var mesh=new Builder();mesh.Box(new Vector3(0,-.13f,0),new Vector3(170,.2f,125));return mesh.Build("Grass stadium surround");}
        public static Mesh EndStand(int side){var mesh=new Builder();for(int row=0;row<10;row++)mesh.Box(new Vector3(side*(60+row*1.15f),row*.7f,0),new Vector3(1.2f,.65f,88));return mesh.Build("Goal end terrace");}
        public static Mesh PerimeterBoards(){var mesh=new Builder();for(int side=-1;side<=1;side+=2){mesh.Box(new Vector3(0,.42f,side*36.5f),new Vector3(108,.8f,.16f));mesh.Box(new Vector3(side*56,.42f,0),new Vector3(.16f,.8f,71));}return mesh.Build("Perimeter boards");}
        public static Mesh TechnicalArea()
        {
            var mesh=new Builder();
            // Low benches on the broadcast side never obstruct the pitch or touchline.
            for(int side=-1;side<=1;side+=2){
                float x=side*11;mesh.Box(new Vector3(x,.26f,37.7f),new Vector3(8.2f,.18f,.55f));mesh.Box(new Vector3(x,.65f,38.05f),new Vector3(8.2f,.7f,.12f));
                for(int seat=-3;seat<=3;seat++)mesh.Box(new Vector3(x+seat*1.05f,.65f,37.65f),new Vector3(.07f,.6f,.55f));
                mesh.Box(new Vector3(side*3,.48f,37.5f),new Vector3(1.8f,.8f,.85f));
            }
            for(int end=-1;end<=1;end+=2)for(int side=-1;side<=1;side+=2)mesh.Beam(new Vector3(end*52.5f,0,side*34),new Vector3(end*52.5f,1.5f,side*34),.035f);
            return mesh.Build("Grouped benches and corner posts");
        }
        public static Mesh ClubBanners()
        {
            var mesh=new Builder();
            for(int side=-1;side<=1;side+=2){
                for(int block=0;block<6;block++)mesh.Box(new Vector3(-47+block*18.8f,.43f,side*36.39f),new Vector3(8,.36f,.035f));
                for(int end=-1;end<=1;end+=2)mesh.Box(new Vector3(end*52.5f+.18f,1.34f,side*34),new Vector3(.34f,.26f,.035f));
            }
            return mesh.Build("Club colour boards and corner flags");
        }
        public static Mesh GoalNet(int side)
        {
            var mesh=new Builder();float front=side*52.5f,back=front+side*1.8f;
            for(int column=0;column<=25;column++){
                float z=Mathf.Lerp(-3.66f,3.66f,column/25f);
                mesh.Beam(new Vector3(front,2.44f,z),new Vector3(back,2.2f,z),.018f);
                mesh.Beam(new Vector3(back,2.2f,z),new Vector3(back,.1f,z),.018f);
            }
            for(int row=0;row<=8;row++){
                float t=row/8f;mesh.Beam(new Vector3(back,Mathf.Lerp(.1f,2.2f,t),-3.66f),new Vector3(back,Mathf.Lerp(.1f,2.2f,t),3.66f),.018f);
                for(int edge=-1;edge<=1;edge+=2)mesh.Beam(new Vector3(front,Mathf.Lerp(.1f,2.44f,t),edge*3.66f),new Vector3(back,Mathf.Lerp(.1f,2.2f,t),edge*3.66f),.018f);
            }
            for(int depth=1;depth<6;depth++){
                float t=depth/6f,x=Mathf.Lerp(front,back,t),top=Mathf.Lerp(2.44f,2.2f,t);
                mesh.Beam(new Vector3(x,top,-3.66f),new Vector3(x,top,3.66f),.018f);
                for(int edge=-1;edge<=1;edge+=2)mesh.Beam(new Vector3(x,.1f,edge*3.66f),new Vector3(x,top,edge*3.66f),.018f);
            }
            return mesh.Build("Woven goal net");
        }
        sealed class Builder
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});}
            public void GroundPath(Vector3[] points){for(int i=1;i<points.Length;i++){var a=points[i-1];var b=points[i];a.y=b.y=.025f;var width=Vector3.Cross((b-a).normalized,Vector3.up)*.05f;Quad(a-width,a+width,b+width,b-width);}}
            public void Arc(Vector3 center,float radius,float start,float end,int segments){var path=new Vector3[segments+1];for(int i=0;i<=segments;i++){float angle=Mathf.Lerp(start,end,i/(float)segments);path[i]=center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);}GroundPath(path);}
            public void Spot(Vector3 center){center.y=.025f;for(int i=0;i<16;i++){float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;int index=vertices.Count;vertices.Add(center);vertices.Add(center+new Vector3(Mathf.Cos(b)*.11f,0,Mathf.Sin(b)*.11f));vertices.Add(center+new Vector3(Mathf.Cos(a)*.11f,0,Mathf.Sin(a)*.11f));triangles.AddRange(new[]{index,index+1,index+2});}}
            public void Beam(Vector3 start,Vector3 end,float width)
            {
                var direction=(end-start).normalized;var u=Vector3.Cross(direction,Vector3.up);if(u.sqrMagnitude<.001f)u=Vector3.right;u=u.normalized*(width*.5f);var v=Vector3.Cross(direction,u);
                Quad(start-u-v,start+u-v,end+u-v,end-u-v);Quad(start+u-v,start+u+v,end+u+v,end+u-v);Quad(start+u+v,start-u+v,end-u+v,end+u+v);Quad(start-u+v,start-u-v,end-u-v,end-u+v);
                Quad(start-u+v,start+u+v,start+u-v,start-u-v);Quad(end-u-v,end+u-v,end+u+v,end-u+v);
            }
            public void Box(Vector3 center,Vector3 size)
            {
                var p=center-size*.5f;var q=center+size*.5f;
                Quad(new Vector3(p.x,p.y,q.z),new Vector3(q.x,p.y,q.z),new Vector3(q.x,q.y,q.z),new Vector3(p.x,q.y,q.z));
                Quad(new Vector3(q.x,p.y,p.z),new Vector3(p.x,p.y,p.z),new Vector3(p.x,q.y,p.z),new Vector3(q.x,q.y,p.z));
                Quad(new Vector3(p.x,p.y,p.z),new Vector3(p.x,p.y,q.z),new Vector3(p.x,q.y,q.z),new Vector3(p.x,q.y,p.z));
                Quad(new Vector3(q.x,p.y,q.z),new Vector3(q.x,p.y,p.z),new Vector3(q.x,q.y,p.z),new Vector3(q.x,q.y,q.z));
                Quad(new Vector3(p.x,q.y,q.z),new Vector3(q.x,q.y,q.z),new Vector3(q.x,q.y,p.z),new Vector3(p.x,q.y,p.z));
                Quad(new Vector3(p.x,p.y,p.z),new Vector3(q.x,p.y,p.z),new Vector3(q.x,p.y,q.z),new Vector3(p.x,p.y,q.z));
            }
            public Mesh Build(string name){var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }
    }
}
