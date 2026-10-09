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
        public const float DefaultOccupancy=.84f; // part des sièges occupés
        public static Mesh Crowd(string home,string away,float occupancy=DefaultOccupancy)=>Crowd(home,away,occupancy,out _);
        // rig : découpage du maillage par supporter (pour CrowdReaction), sans objet par spectateur.
        public static Mesh Crowd(string home,string away,float occupancy,out CrowdRig rig)
        {
            occupancy=float.IsNaN(occupancy)||float.IsInfinity(occupancy)?0:Mathf.Clamp01(occupancy);
            uint state=2166136261;foreach(char c in (home??"")+"|"+(away??"")){state^=c;state*=16777619;}
            var builder=new CrowdBuilder();
            uint Next(){state=state*1664525+1013904223;return state;}
            void Add(Vector3 position,Vector3 towardPitch,bool visiting,bool detailed,bool homeEnd=false)
            {
                uint sample=Next();if((sample&65535)/65536f>=occupancy)return;
                float height=.44f+((sample>>16)&15)*.012f;
                int clothing=visiting&&sample%4==0?4:(int)((sample>>21)%4);
                // Bits mélangés : les bits bas d'un générateur congruentiel ont une période courte.
                uint mix=sample*2654435761u;
                // Virage populaire : trois habits neutres sur quatre passent aux couleurs du club.
                if(homeEnd&&clothing>1&&(mix>>30)!=0)clothing=(int)((mix>>29)&1);
                // Tribunes latérales : quelques maillots du club au milieu des habits neutres.
                else if(!homeEnd&&!visiting&&clothing>1&&((mix>>22)&7)==0)clothing=(int)((mix>>25)&1);
                int first=builder.VertexCount;
                // Virage populaire, au-delà des deux premiers rangs : un supporter sur quatre debout.
                bool standing=homeEnd&&!detailed&&((mix>>22)&3)==0;var torso=standing?position+Vector3.up*(StandingHip-SeatHeight):position;
                if(standing)builder.Standing(position-Vector3.up*SeatHeight,towardPitch,StandingHip,height,clothing,5+(int)((sample>>25)&1),sample);
                else builder.Person(position,towardPitch,height,clothing,5+(int)((sample>>25)&1),sample,detailed);
                if(homeEnd&&(mix>>27&3)<HomeEndScarfQuarters)builder.Scarf(torso,towardPitch,height,(int)((mix>>26)&1));
                builder.EndPerson(first,position,visiting);
            }
            for(int side=-1;side<=1;side+=2){
                for(int row=0;row<9;row++)for(int block=0;block<6;block++)for(int seat=0;seat<14;seat++){
                    float x=-47+block*18.8f-7.8f+seat*1.2f;
                    Add(new Vector3(x,row*.65f+.61f,side*(39+row*1.1f)),new Vector3(0,0,-side),false,row<2);
                }
                for(int row=0;row<10;row++)for(int block=0;block<4;block++)for(int seat=0;seat<15;seat++){
                    float z=-31.5f+block*21-8.4f+seat*1.2f;
                    Add(new Vector3(side*(60+row*1.15f),row*.7f+.39f,z),new Vector3(-side,0,0),side==1&&block==3,row<2,side==HomeEndSide);
                }
            }
            // Bancs : remplaçants (survêtement du club) et un membre du staff assis, entraîneur debout devant.
            for(int side=-1;side<=1;side+=2){
                bool away=side>0;int kit=away?AwayBenchClothing:HomeBenchClothing;
                for(int seat=0;seat<StadiumGeometry.BenchSeats;seat++){
                    uint sample=Next();var position=new Vector3(side*StadiumGeometry.BenchX+(seat-(StadiumGeometry.BenchSeats-1)*.5f)*StadiumGeometry.BenchSeatPitch,StadiumGeometry.BenchSeatTop,StadiumGeometry.BenchSeatZ+BenchBackOffset);
                    int first=builder.VertexCount;builder.Person(position,Vector3.back,.46f+((sample>>16)&7)*.01f,seat==StadiumGeometry.BenchSeats-1?StaffClothing:kit,5+(int)((sample>>25)&1),sample,true);builder.EndPerson(first,position,away);
                }
                uint coach=Next();var feet=new Vector3(side*(StadiumGeometry.BenchX-StadiumGeometry.BenchSeats*.5f*StadiumGeometry.BenchSeatPitch-CoachSideOffset),0,CoachZ);
                int start=builder.VertexCount;builder.Standing(feet,Vector3.back,StandingHip,.5f,StaffClothing,5+(int)((coach>>25)&1),coach);builder.EndPerson(start,feet+Vector3.up*StandingHip,away);
            }
            rig=builder.Rig();return builder.Build();
        }
        const float SeatHeight=.39f;       // m : hanches au-dessus de la marche (Add place les sièges à +0,39 m dans les virages)
        const float StandingHip=.88f;      // m : hauteur des hanches d'un supporter debout
        const int HomeBenchClothing=0,AwayBenchClothing=4,StaffClothing=2; // matières de Palette : club recevant, visiteur, sombre
        const float BenchBackOffset=.05f;  // m : buste un peu en arrière du milieu du siège
        const float CoachZ=37f,CoachSideOffset=1.2f; // m : entraîneur debout entre panneaux (36,5) et banc, à côté du banc
        public const int HomeEndSide=-1;          // virage des supporters du club recevant (x < 0)
        const uint HomeEndScarfQuarters=1;        // quarts du virage populaire qui portent une écharpe
        public const float ScarfRaise=.5f;        // m : écharpe tendue au-dessus de la tête (célébration)
        public const float ElbowRaise=.55f,HandRaise=.9f; // m : bras levés (premiers rangs détaillés)
        // Second anneau de la tribune d'en face : silhouettes simples (cartes), maillage à part.
        public static Mesh UpperCrowd(string home,string away,float occupancy=.84f)=>UpperCrowd(home,away,occupancy,out _);
        // rig : tous rattachés au club recevant (CrowdReaction : debout et sauts sur ses buts ; cartes sans bras).
        public static Mesh UpperCrowd(string home,string away,float occupancy,out CrowdRig rig)
        {
            occupancy=float.IsNaN(occupancy)||float.IsInfinity(occupancy)?0:Mathf.Clamp01(occupancy);
            uint state=2166136261;foreach(char c in "upper|"+(home??"")+"|"+(away??"")){state^=c;state*=16777619;}
            uint Next(){state=state*1664525+1013904223;return state;}
            var builder=new CrowdBuilder();
            for(int row=0;row<StadiumGeometry.UpperTierRows;row++)for(int seat=0;seat<UpperSeatsPerRow;seat++){
                uint sample=Next();if((sample&65535)/65536f>=occupancy)continue;
                var position=new Vector3(-57+seat*1.2f,StadiumGeometry.UpperTierBase+row*StadiumGeometry.UpperRowRise,-(StadiumGeometry.UpperTierFront+row*StadiumGeometry.UpperRowDepth));
                int first=builder.VertexCount;
                builder.Person(position,Vector3.forward,.44f+((sample>>16)&15)*.012f,(int)((sample>>21)%4),5+(int)((sample>>25)&1),sample,false);
                builder.EndPerson(first,position,false);
            }
            rig=builder.Rig();var mesh=builder.Build();mesh.name="Upper tier supporters";return mesh;
        }
        const int UpperSeatsPerRow=96; // sièges de 1,2 m sur 115 m

        // Atlas des panneaux publicitaires, généré (aucune image) : une ligne de
        // BoardRowPixels par motif, quatre motifs par club, formes inventées sans
        // texte ni marque. Bordure sombre de 2 px par ligne (cadre et marge de mipmap).
        public const int BoardDesignsPerClub=4,BoardDesigns=2*BoardDesignsPerClub;
        public const int BoardAtlasWidth=256,BoardRowPixels=32,BoardAtlasHeight=BoardDesigns*BoardRowPixels;
        const int BoardBorder=2; // px
        public static readonly Color BoardFrame=new Color(.05f,.055f,.065f),BoardNavy=new Color(.06f,.08f,.13f);
        static readonly Color LightInk=new Color(.95f,.95f,.92f),DarkInk=new Color(.07f,.08f,.1f);
        // Encre contrastée avec la couleur du club (claire sur couleur sombre et inversement).
        public static Color Contrast(Color club)=>club.r*.3f+club.g*.59f+club.b*.11f>.55f?DarkInk:LightInk;
        public static Vector4 BoardUv(int design)
        {
            float v0=(design*BoardRowPixels+BoardBorder)/(float)BoardAtlasHeight,v1=((design+1)*BoardRowPixels-BoardBorder)/(float)BoardAtlasHeight;
            return new Vector4(0,v0,1,v1);
        }
        // Couleur d'un pixel du motif design (x : 0–255, y : 0–31 dans la ligne).
        public static Color BoardPixel(int design,int x,int y,Color home,Color away)
        {
            if(y<BoardBorder||y>=BoardRowPixels-BoardBorder)return BoardFrame;
            var club=design<BoardDesignsPerClub?home:away;club.a=1;var ink=Contrast(club);
            float cy=y-BoardRowPixels*.5f+.5f; // px depuis l'axe du panneau
            switch(design%BoardDesignsPerClub){
                case 0: // chevrons
                    return Mathf.Repeat(x+Mathf.Abs(cy)*1.6f,48)<12?ink:club;
                case 1:{ // pastilles cerclées sur fond nuit
                    float dx=Mathf.Repeat(x-26,52)-26,r=Mathf.Sqrt(dx*dx+cy*cy);
                    return r<5?ink:r<10?club:Mathf.Abs(cy)<1.5f?club:BoardNavy;}
                case 2: // biseau aux couleurs du club et trois carrés
                    if(x<96+cy*1.2f)return club;
                    return Mathf.Abs(cy)<6&&x>=132&&x<240&&Mathf.Repeat(x-132,36)<12?club:ink;
                default: // vague
                    return Mathf.Abs(cy-7*Mathf.Sin(2*Mathf.PI*x/128f))<3.5f?ink:club;
            }
        }
        public static Texture2D BoardAtlas(Color home,Color away)
        {
            var texture=new Texture2D(BoardAtlasWidth,BoardAtlasHeight,TextureFormat.RGB24,true){name="Advertising board atlas",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color32[BoardAtlasWidth*BoardAtlasHeight];
            for(int design=0;design<BoardDesigns;design++)for(int y=0;y<BoardRowPixels;y++)for(int x=0;x<BoardAtlasWidth;x++)
                pixels[(design*BoardRowPixels+y)*BoardAtlasWidth+x]=BoardPixel(design,x,y,home,away);
            texture.SetPixels32(pixels);texture.Apply(true,true);
            return texture;
        }
        sealed class CrowdBuilder
        {
            static readonly int[] TorsoIndices={0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0,4,7,6,4,6,5};
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Vector3> cardNormals=new List<Vector3>();
            readonly List<float> raise=new List<float>(); // m de levée de chaque sommet quand les bras montent
            readonly List<int> personStart=new List<int>();readonly List<Vector3> personSeat=new List<Vector3>();readonly List<bool> personVisiting=new List<bool>();
            readonly List<int>[] triangles=new List<int>[MaterialCount];
            public CrowdBuilder(){for(int i=0;i<MaterialCount;i++)triangles[i]=new List<int>();}
            public int VertexCount=>vertices.Count;
            int Vertex(Vector3 point,Vector3 cardNormal=default){int index=vertices.Count;vertices.Add(point);cardNormals.Add(cardNormal);raise.Add(0);return index;}
            public void EndPerson(int first,Vector3 seat,bool visiting){if(vertices.Count==first)return;personStart.Add(first);personSeat.Add(seat);personVisiting.Add(visiting);}
            public CrowdRig Rig()=>new CrowdRig(personStart.ToArray(),personSeat.ToArray(),personVisiting.ToArray(),raise.ToArray());
            // Écharpe au cou, une seule face tournée vers le terrain ; levée au-dessus de la tête en célébration.
            public void Scarf(Vector3 seat,Vector3 facing,float height,int material)
            {
                var right=Vector3.Cross(Vector3.up,facing);var centre=seat+Vector3.up*(height-.03f)+facing*.1f;
                const float halfWidth=.31f,halfHeight=.055f; // m
                int start=vertices.Count;var normal=(facing+Vector3.up*.12f).normalized;
                Vertex(centre-right*halfWidth-Vector3.up*halfHeight,normal);Vertex(centre+right*halfWidth-Vector3.up*halfHeight,normal);
                Vertex(centre+right*halfWidth+Vector3.up*halfHeight,normal);Vertex(centre-right*halfWidth+Vector3.up*halfHeight,normal);
                for(int i=start;i<start+4;i++)raise[i]=ScarfRaise;
                // Face avant : sens horaire vu du terrain.
                if(Vector3.Dot(Vector3.Cross(vertices[start+1]-vertices[start],vertices[start+2]-vertices[start]),normal)>0)Quad(material,start,start+1,start+2,start+3);
                else Quad(material,start,start+3,start+2,start+1);
            }
            // Silhouette debout : deux jambes en cartes, buste et tête simples (comme les rangs éloignés).
            public void Standing(Vector3 feet,Vector3 facing,float hip,float torso,int clothing,int skin,uint variation)
            {
                var right=Vector3.Cross(Vector3.up,facing);var normal=(facing+Vector3.up*.12f).normalized;
                for(int side=-1;side<=1;side+=2){var x=right*(side*.075f);Card(2,normal,feet+x-right*.045f,feet+x+right*.045f,feet+x+right*.05f+Vector3.up*hip,feet+x-right*.05f+Vector3.up*hip);}
                Person(feet+Vector3.up*hip,facing,torso,clothing,skin,variation,false);
            }
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
                    int arm=vertices.Count;
                    Card(((variation>>26)&1)==0?clothing:skin,normal,At(side*shoulder,height-.035f,lean+.035f),At(side*elbow,height*.37f,.14f),At(side*.14f,.065f,handDepth),At(side*(elbow-.055f),height*.38f,.15f));
                    raise[arm+1]=raise[arm+3]=ElbowRaise;raise[arm+2]=HandRaise;
                    float x=side*.09f;Card(2,normal,At(x-.043f,.005f,.08f),At(x+.043f,.005f,.08f),At(x+.043f,-.065f,.31f),At(x-.043f,-.065f,.31f));
                }
            }
            public Mesh Build(){var mesh=new Mesh{name="Batched seated supporters",subMeshCount=MaterialCount};mesh.SetVertices(vertices);for(int i=0;i<MaterialCount;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();var normals=mesh.normals;for(int i=0;i<normals.Length;i++)if(cardNormals[i].sqrMagnitude>.1f)normals[i]=cardNormals[i];mesh.normals=normals;mesh.RecalculateBounds();return mesh;}
        }
    }
}
