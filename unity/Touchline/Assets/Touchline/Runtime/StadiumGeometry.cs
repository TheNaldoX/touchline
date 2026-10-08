using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    // Each static bank/net is one surface, rather than dozens of renderers.
    // The net has physical thickness and remains visible from every camera.
    public static class StadiumGeometry
    {
        // shaded : seulement les traits à l'ombre simulée du toit (x < shadeEndX, z < shadeEdge) ;
        // sinon les autres. Sans ombre (valeurs par défaut) : tous les traits.
        public static Mesh PitchMarkings(bool shaded=false,float shadeEdge=float.NegativeInfinity,float shadeEndX=float.NegativeInfinity)
        {
            var mesh=new Builder{Keep=p=>(p.z<shadeEdge&&p.x<shadeEndX)==shaded,SplitZ=shadeEdge,SplitX=shadeEndX};
            mesh.GroundPath(new[]{new Vector3(-52.5f,0,-34),new Vector3(52.5f,0,-34),new Vector3(52.5f,0,34),new Vector3(-52.5f,0,34),new Vector3(-52.5f,0,-34)});
            mesh.GroundPath(new[]{new Vector3(0,0,-34),new Vector3(0,0,34)});mesh.Arc(Vector3.zero,9.15f,0,Mathf.PI*2,64);mesh.Spot(Vector3.zero);
            for(int side=-1;side<=1;side+=2){
                float goal=side*52.5f;var spot=new Vector3(side*41.5f,0,0);mesh.Spot(spot);
                mesh.GroundPath(new[]{new Vector3(goal,0,-20.16f),new Vector3(goal-side*16.5f,0,-20.16f),new Vector3(goal-side*16.5f,0,20.16f),new Vector3(goal,0,20.16f)});
                mesh.GroundPath(new[]{new Vector3(goal,0,-9.16f),new Vector3(goal-side*5.5f,0,-9.16f),new Vector3(goal-side*5.5f,0,9.16f),new Vector3(goal,0,9.16f)});
                float angle=Mathf.Acos(5.5f/9.15f),center=side>0?Mathf.PI:0;mesh.Arc(spot,9.15f,center-angle,center+angle,24);
                for(int edge=-1;edge<=1;edge+=2){float start=side>0?(edge>0?Mathf.PI:Mathf.PI*.5f):(edge>0?-Mathf.PI*.5f:0);mesh.Arc(new Vector3(goal,0,edge*34),1,start,start+Mathf.PI*.5f,12);}
            }
            return mesh.Build(shaded?"Pitch markings in roof shadow":"Regulation pitch markings");
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
        public const float RoofWidth=124f,RoofThickness=.35f;                  // m (toit centré en x)
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
            mesh.Box(new Vector3(0,RoofHeight,-(RoofFront+StadiumBack)*.5f),new Vector3(RoofWidth,RoofThickness,StadiumBack-RoofFront));
            mesh.Box(new Vector3(0,RoofHeight*.5f,-StadiumBack),new Vector3(RoofWidth,RoofHeight,.5f));
            for(int i=-3;i<=3;i++)mesh.Beam(new Vector3(i*19f,UpperTierBase+UpperTierRows*UpperRowRise,-(StadiumBack-.5f)),new Vector3(i*19f,RoofHeight,-(StadiumBack-.5f)),.35f);
            // Murs derrière les virages (au-dessus du dernier rang, hauteur 9 m).
            for(int side=-1;side<=1;side+=2)mesh.Box(new Vector3(side*72f,4.5f,0),new Vector3(.5f,9f,90));
            return mesh.Build("Stadium roof and back walls");
        }
        // Pelouse autour du terrain (170 × 125 m). shaded : seulement le coin à l'ombre du
        // toit (x < shadeEndX, z < shadeEdge) ; sinon le reste. Sans ombre : tout.
        public const float SurroundHalfX=85f,SurroundHalfZ=62.5f; // m
        public static Mesh Surround(bool shaded=false,float shadeEdge=-SurroundHalfZ,float shadeEndX=-SurroundHalfX)
        {
            var mesh=new Builder();shadeEdge=Mathf.Clamp(shadeEdge,-SurroundHalfZ,SurroundHalfZ);shadeEndX=Mathf.Clamp(shadeEndX,-SurroundHalfX,SurroundHalfX);
            void Slab(float x0,float x1,float z0,float z1){if(x1-x0>.01f&&z1-z0>.01f)mesh.Box(new Vector3((x0+x1)*.5f,-.13f,(z0+z1)*.5f),new Vector3(x1-x0,.2f,z1-z0));}
            if(shaded)Slab(-SurroundHalfX,shadeEndX,-SurroundHalfZ,shadeEdge);
            else{Slab(-SurroundHalfX,SurroundHalfX,shadeEdge,SurroundHalfZ);Slab(shadeEndX,SurroundHalfX,-SurroundHalfZ,shadeEdge);}
            return mesh.Build(shaded?"Grass surround in roof shadow":"Grass stadium surround");
        }
        public static Mesh EndStand(int side){var mesh=new Builder();for(int row=0;row<10;row++)mesh.Box(new Vector3(side*(60+row*1.15f),row*.7f,0),new Vector3(1.2f,.65f,88));return mesh.Build("Goal end terrace");}
        // Panneaux publicitaires : un seul maillage, une seule matière. La face côté
        // terrain porte des panneaux qui alternent les couleurs des deux clubs ; leurs
        // motifs viennent de l'atlas généré (StadiumAtmosphere.BoardAtlas).
        public const float BoardHeight=.8f,BoardBase=.02f,BoardThickness=.16f; // m
        public const float SideBoardLine=36.5f,EndBoardLine=56f;               // m : axe des panneaux (distance au centre)
        public const float SideBoardLength=108f,EndBoardLength=71f;            // m
        public const int SidePanels=18,EndPanels=12;                           // panneaux de 6 m et 5,9 m
        const float PanelInset=.015f,PanelMargin=.04f;                          // m : décollement devant la face (pas de scintillement), cadre haut/bas
        // Motif d'un panneau : pairs = club recevant (lignes 0–3), impairs = visiteur (4–7).
        public static int BoardDesign(int run,int panel)=>(panel&1)*StadiumAtmosphere.BoardDesignsPerClub+(panel/2+run)%StadiumAtmosphere.BoardDesignsPerClub;
        public static Mesh PerimeterBoards()
        {
            var mesh=new Builder();int run=0;
            for(int side=-1;side<=1;side+=2){
                mesh.Box(new Vector3(0,BoardBase+BoardHeight*.5f,side*SideBoardLine),new Vector3(SideBoardLength,BoardHeight,BoardThickness));
                mesh.Box(new Vector3(side*EndBoardLine,BoardBase+BoardHeight*.5f,0),new Vector3(BoardThickness,BoardHeight,EndBoardLength));
                BoardPanels(mesh,new Vector3(0,0,side*(SideBoardLine-BoardThickness*.5f-PanelInset)),new Vector3(0,0,-side),SideBoardLength,SidePanels,run++);
                BoardPanels(mesh,new Vector3(side*(EndBoardLine-BoardThickness*.5f-PanelInset),0,0),new Vector3(-side,0,0),EndBoardLength,EndPanels,run++);
            }
            return mesh.Build("Perimeter boards");
        }
        static void BoardPanels(Builder mesh,Vector3 line,Vector3 towardPitch,float length,int count,int run)
        {
            var right=Vector3.Cross(Vector3.up,-towardPitch);float width=length/count,height=BoardHeight-2*PanelMargin;
            for(int i=0;i<count;i++){
                var center=line+right*(-length*.5f+(i+.5f)*width)+Vector3.up*(BoardBase+BoardHeight*.5f);
                mesh.Panel(center,towardPitch,width,height,StadiumAtmosphere.BoardUv(BoardDesign(run,i)));
            }
        }
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
                for(int end=-1;end<=1;end+=2)mesh.Box(new Vector3(end*52.5f+.18f,1.34f,side*34),new Vector3(.34f,.26f,.035f));
            }
            return mesh.Build("Corner flags");
        }
        // Pylônes d'éclairage derrière les quatre coins (hors du champ de la caméra
        // télé côté +Z) et rampe sous le bord du toit d'en face.
        public const float MastX=76f,MastZ=44f,MastHeight=34f;   // m
        public const float MastHeadWidth=9f,MastHeadHeight=4f;   // m
        const int HeadColumns=6,HeadRows=3;                       // lampes par tête
        public const int RoofLamps=24;
        const float RoofLampWidth=2.2f,RoofLampHeight=.5f,RoofLampSpan=57.5f; // m (rampe de -57,5 à 57,5 m)
        static Vector3 MastHead(int x,int z)=>new Vector3(x*MastX,MastHeight,z*MastZ);
        static Vector3 HeadFacing(Vector3 head)=>(-head).normalized; // vers le rond central
        public static Mesh FloodlightMasts()
        {
            var mesh=new Builder();
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){
                var head=MastHead(x,z);var facing=HeadFacing(head);
                mesh.Beam(new Vector3(head.x,0,head.z),head-facing*.3f,.8f);
                // Plaque sombre derrière les lampes, et dos du caisson.
                mesh.Panel(head-facing*.05f,facing,MastHeadWidth+.4f,MastHeadHeight+.4f,Vector4.zero);
                mesh.Panel(head-facing*.25f,-facing,MastHeadWidth+.4f,MastHeadHeight+.4f,Vector4.zero);
            }
            return mesh.Build("Floodlight masts");
        }
        public static Mesh FloodlightLamps()
        {
            var mesh=new Builder();
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){
                var head=MastHead(x,z);var facing=HeadFacing(head);
                var right=Vector3.Cross(Vector3.up,-facing).normalized;var up=Vector3.Cross(-facing,right);
                float cellX=MastHeadWidth/HeadColumns,cellY=MastHeadHeight/HeadRows;
                for(int c=0;c<HeadColumns;c++)for(int r=0;r<HeadRows;r++)
                    mesh.Panel(head+right*(-MastHeadWidth*.5f+(c+.5f)*cellX)+up*(-MastHeadHeight*.5f+(r+.5f)*cellY),facing,cellX*.8f,cellY*.75f,Vector4.zero);
            }
            var roofFacing=new Vector3(0,-.5f,1).normalized;
            for(int i=0;i<RoofLamps;i++){
                float x=Mathf.Lerp(-RoofLampSpan,RoofLampSpan,i/(RoofLamps-1f));
                mesh.Panel(new Vector3(x,RoofHeight-.45f,-(RoofFront+.3f)),roofFacing,RoofLampWidth,RoofLampHeight,Vector4.zero);
            }
            return mesh.Build("Floodlight lamps");
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
            // UV seulement si un panneau texturé en a besoin ; ailleurs (0,0) = cadre de l'atlas.
            readonly List<Vector2> uvs=new List<Vector2>();bool textured;
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector4 uv=default)
            {
                int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
                uvs.Add(new Vector2(uv.z,uv.y));uvs.Add(new Vector2(uv.x,uv.y));uvs.Add(new Vector2(uv.x,uv.w));uvs.Add(new Vector2(uv.z,uv.w));
            }
            // Rectangle tourné vers facing ; uv = (u0, v0, u1, v1), u croissant vers la droite de qui le regarde.
            public void Panel(Vector3 center,Vector3 facing,float width,float height,Vector4 uv)
            {
                var right=Vector3.Cross(Vector3.up,-facing).normalized*(width*.5f);var up=Vector3.Cross(-facing,right).normalized*(height*.5f);
                if(uv!=Vector4.zero)textured=true;
                Quad(center+right-up,center-right-up,center-right+up,center+right+up,uv);
            }
            // Filtre facultatif des traits au sol : un segment est coupé en x = SplitX et
            // z = SplitZ, chaque morceau gardé si Keep(son milieu).
            public System.Func<Vector3,bool> Keep;public float SplitX=float.NegativeInfinity,SplitZ=float.NegativeInfinity;
            public void GroundPath(Vector3[] points)
            {
                for(int i=1;i<points.Length;i++){
                    var a=points[i-1];var b=points[i];a.y=b.y=.025f;var side=Vector3.Cross((b-a).normalized,Vector3.up);
                    if(Keep==null){Quad(a-side*HalfLine(a),a+side*HalfLine(a),b+side*HalfLine(b),b-side*HalfLine(b));continue;}
                    var cuts=new List<float>{0,1};
                    if((a.x-SplitX)*(b.x-SplitX)<0)cuts.Add((SplitX-a.x)/(b.x-a.x));
                    if((a.z-SplitZ)*(b.z-SplitZ)<0)cuts.Add((SplitZ-a.z)/(b.z-a.z));
                    cuts.Sort();
                    for(int c=1;c<cuts.Count;c++){var p=Vector3.Lerp(a,b,cuts[c-1]);var q=Vector3.Lerp(a,b,cuts[c]);if(cuts[c]-cuts[c-1]>1e-5f&&Keep((p+q)*.5f))Quad(p-side*HalfLine(p),p+side*HalfLine(p),q+side*HalfLine(q),q-side*HalfLine(q));}
                }
            }
            // Demi-largeur d'un trait (m) : 10 cm sur la touche côté caméra principale, élargie
            // linéairement jusqu'à 12 cm (maximum réglementaire) sur la touche opposée, vue à
            // plus de 60 m, pour qu'elle reste continue à l'écran au lieu de se morceler.
            // Linéaire en z : un trait coupé à l'ombre du toit garde exactement la même surface.
            const float LineHalfWidth=.05f,FarLineExtra=.01f,TouchlineZ=34f;
            static float HalfLine(Vector3 p)=>LineHalfWidth+FarLineExtra*Mathf.Clamp01((TouchlineZ-p.z)/(2*TouchlineZ));
            public void Arc(Vector3 center,float radius,float start,float end,int segments){var path=new Vector3[segments+1];for(int i=0;i<=segments;i++){float angle=Mathf.Lerp(start,end,i/(float)segments);path[i]=center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);}GroundPath(path);}
            public void Spot(Vector3 center){center.y=.025f;if(Keep!=null&&!Keep(center))return;for(int i=0;i<16;i++){float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;int index=vertices.Count;vertices.Add(center);vertices.Add(center+new Vector3(Mathf.Cos(b)*.11f,0,Mathf.Sin(b)*.11f));vertices.Add(center+new Vector3(Mathf.Cos(a)*.11f,0,Mathf.Sin(a)*.11f));triangles.AddRange(new[]{index,index+1,index+2});uvs.Add(default);uvs.Add(default);uvs.Add(default);}}
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
            public Mesh Build(string name){var mesh=new Mesh{name=name};mesh.SetVertices(vertices);if(textured)mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }
    }
}
