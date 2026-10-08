using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    // Flocage du dos (nom + numéro) : une petite grille de sommets épouse le dos
    // du maillot (mêmes poids de peau, donc elle suit le torse) et lit une case
    // d'un atlas commun à tous les joueurs. Chaque joueur choisit sa case par le
    // décalage de texture de son matériau : pas de texture par joueur.
    public static class ShirtPrint
    {
        public const int CellWidth=128,CellHeight=160,Columns=8,Rows=4; // px ; 32 cases
        public const int AtlasWidth=CellWidth*Columns,AtlasHeight=CellHeight*Rows;
        const int AtlasMips=4;                    // niveaux de mip limités : peu de mélange entre cases
        // Zone du flocage sur le dos, pose de repos (m) : largeur / hauteur = 128 / 160.
        public const float Left=-.14f,Right=.14f,Bottom=1.12f,Top=1.47f;
        const float Lift=.004f;                   // m au-dessus du tissu (pas de scintillement)
        const int GridX=9,GridY=11;               // sommets de la grille (suit les courbes du dos)
        const int Margin=4;                       // px transparents au bord de chaque case
        const float NumberBottom=1.15f,NumberTop=1.37f,NameBottom=1.405f,NameTop=1.458f; // m
        const float NumberStroke=.15f,NameStroke=.15f; // épaisseur du trait, en hauteurs de glyphe
        const float NumberOutline=2.5f,NameOutline=1f; // px de contour
        const float MinimumNameSqueeze=.55f,MinimumNumberSqueeze=.72f; // compression horizontale maximale (nom long, numéro à 2 chiffres)

        static Texture2D atlas;
        static readonly string[] keys=new string[Columns*Rows];
        static readonly int[] users=new int[Columns*Rows];
        static bool dirty;

        public static Texture2D Atlas{get{
            if(atlas==null){
                atlas=new Texture2D(AtlasWidth,AtlasHeight,TextureFormat.RGBA32,AtlasMips,false){name="Shirt lettering atlas",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
                atlas.SetPixelData(new Color32[AtlasWidth*AtlasHeight],0);atlas.Apply(true);
                for(int i=0;i<keys.Length;i++){keys[i]=null;users[i]=0;}
            }
            return atlas;}}
        public static Vector2 CellScale=>new Vector2(1f/Columns,1f/Rows);
        public static Vector2 CellOffset(int cell)=>new Vector2((cell%Columns)/(float)Columns,(cell/Columns)/(float)Rows);

        // Réserve (ou partage) la case qui porte ce flocage ; -1 si l'atlas est plein.
        public static int Acquire(string surname,int number,Color fill,Color outline)
        {
            var tex=Atlas;string key=surname+"|"+number+"|"+ColorUtility.ToHtmlStringRGB(fill)+ColorUtility.ToHtmlStringRGB(outline);
            for(int i=0;i<keys.Length;i++)if(keys[i]==key){users[i]++;return i;}
            for(int i=0;i<keys.Length;i++)if(users[i]==0){keys[i]=key;users[i]=1;Draw(tex,i,surname,number,fill,outline);return i;}
            return -1;
        }
        public static void Release(int cell){if(cell>=0&&cell<users.Length&&users[cell]>0)users[cell]--;}
        public static int Users(int cell)=>cell>=0&&cell<users.Length?users[cell]:0;
        // Envoie l'atlas au GPU une seule fois après une série de changements.
        public static void Flush(){if(dirty&&atlas!=null){atlas.Apply(true);dirty=false;}}

        static void Draw(Texture2D tex,int cell,string surname,int number,Color fill,Color outline)
        {
            var distance=new float[CellWidth*CellHeight];
            var pixels=new Color32[CellWidth*CellHeight];
            float pxPerMetre=CellHeight/(Top-Bottom);
            if(number>0)Layer(pixels,distance,number.ToString(),NumberBottom,NumberTop,NumberStroke,NumberOutline,MinimumNumberSqueeze,pxPerMetre,fill,outline);
            if(!string.IsNullOrEmpty(surname))Layer(pixels,distance,surname,NameBottom,NameTop,NameStroke,NameOutline,MinimumNameSqueeze,pxPerMetre,fill,outline);
            var data=tex.GetPixelData<Color32>(0);int ox=(cell%Columns)*CellWidth,oy=(cell/Columns)*CellHeight;
            for(int y=0;y<CellHeight;y++)for(int x=0;x<CellWidth;x++)data[(oy+y)*AtlasWidth+ox+x]=pixels[y*CellWidth+x];
            dirty=true;
        }
        static void Layer(Color32[] pixels,float[] distance,string text,float bottom,float top,float stroke,float outline,float minimumSqueeze,float pxPerMetre,Color fill,Color edge)
        {
            float height=(top-bottom)*pxPerMetre;float half=stroke*height*.5f;
            // La boîte du glyphe est réduite de l'épaisseur du trait : le bord extérieur tient dans [bottom, top].
            float glyph=height-2*half;float spacing=ShirtLettering.Spacing+stroke;
            float available=CellWidth-2*(Margin+half+outline);float width=ShirtLettering.Measure(text,spacing)*glyph;
            float squeeze=width>available?Mathf.Max(minimumSqueeze,available/width):1;
            if(width*squeeze>available){float shrink=available/(width*squeeze);glyph*=shrink;half*=shrink;}
            float used=ShirtLettering.Measure(text,spacing)*glyph*squeeze;float left=(CellWidth-used)*.5f;float baseY=bottom*pxPerMetre-Bottom*pxPerMetre+half+(height-2*half-glyph)*.5f;
            for(int i=0;i<distance.Length;i++)distance[i]=float.MaxValue;
            ShirtLettering.Trace(distance,CellWidth,CellHeight,text,left,baseY,glyph,squeeze,spacing,half+outline+1);
            for(int i=0;i<pixels.Length;i++){
                float d=distance[i];if(d==float.MaxValue)continue;
                int x=i%CellWidth,y=i/CellWidth;if(x<Margin||y<Margin||x>=CellWidth-Margin||y>=CellHeight-Margin)continue;
                float inner=Mathf.Clamp01(half-d+.5f),outer=Mathf.Clamp01(half+outline-d+.5f);if(outer<=0)continue;
                var c=Color.Lerp(edge,fill,inner/outer);c.a=outer;
                var previous=(Color)pixels[i];pixels[i]=Color.Lerp(previous,c,c.a>=previous.a?1:c.a);
            }
        }

        // Grille posée sur le dos du maillot : positions, normales, UV (case 0) et poids de peau.
        public static void Patch(HumanPart shirt,out Vector3[] positions,out Vector3[] normals,out Vector2[] uv,out BoneWeight[] weights,out int[] triangles)
        {
            int count=GridX*GridY;positions=new Vector3[count];normals=new Vector3[count];uv=new Vector2[count];weights=new BoneWeight[count];var found=new bool[count];
            int vertices=shirt.position.Length/3;var P=new Vector3[vertices];var N=new Vector3[vertices];
            for(int v=0;v<vertices;v++){P[v]=new Vector3(shirt.position[v*3],shirt.position[v*3+1],shirt.position[v*3+2]);N[v]=new Vector3(shirt.normal[v*3],shirt.normal[v*3+1],shirt.normal[v*3+2]);}
            var index=shirt.index;
            for(int gy=0;gy<GridY;gy++)for(int gx=0;gx<GridX;gx++){
                int g=gy*GridX+gx;float x=Mathf.Lerp(Left,Right,gx/(GridX-1f)),y=Mathf.Lerp(Bottom,Top,gy/(GridY-1f));
                uv[g]=new Vector2(gx/(GridX-1f),gy/(GridY-1f));float bestZ=float.MaxValue;
                for(int t=0;t<index.Length;t+=3){
                    int a=index[t],b=index[t+1],c=index[t+2];
                    if(N[a].z+N[b].z+N[c].z>-.6f)continue; // seulement les faces tournées vers l'arrière
                    var pa=P[a];var pb=P[b];var pc=P[c];
                    float area=(pb.x-pa.x)*(pc.y-pa.y)-(pc.x-pa.x)*(pb.y-pa.y);if(Mathf.Abs(area)<1e-10f)continue;
                    float w1=((x-pa.x)*(pc.y-pa.y)-(pc.x-pa.x)*(y-pa.y))/area,w2=((pb.x-pa.x)*(y-pa.y)-(x-pa.x)*(pb.y-pa.y))/area,w0=1-w1-w2;
                    if(w0<-1e-4f||w1<-1e-4f||w2<-1e-4f)continue;
                    float z=pa.z*w0+pb.z*w1+pc.z*w2;if(z>=bestZ)continue;
                    bestZ=z;found[g]=true;var normal=(N[a]*w0+N[b]*w1+N[c]*w2).normalized;normals[g]=normal;positions[g]=new Vector3(x,y,z)+normal*Lift;
                    weights[g]=Blend(shirt,a,b,c,w0,w1,w2);
                }
            }
            // Point hors du tissu (rare) : on reprend le voisin trouvé le plus proche.
            for(int g=0;g<count;g++)if(!found[g]){int best=-1;float bestDistance=float.MaxValue;
                for(int k=0;k<count;k++)if(found[k]){float d=(uv[k]-uv[g]).sqrMagnitude;if(d<bestDistance){bestDistance=d;best=k;}}
                if(best<0){positions[g]=new Vector3(Mathf.Lerp(Left,Right,uv[g].x),Mathf.Lerp(Bottom,Top,uv[g].y),-.09f);normals[g]=Vector3.back;weights[g]=new BoneWeight{boneIndex0=0,weight0=1};continue;}
                positions[g]=new Vector3(Mathf.Lerp(Left,Right,uv[g].x),Mathf.Lerp(Bottom,Top,uv[g].y),positions[best].z);normals[g]=normals[best];weights[g]=weights[best];}
            // Faces visibles depuis l'arrière (sens horaire vu de −z).
            triangles=new int[(GridX-1)*(GridY-1)*6];int n=0;
            for(int gy=0;gy<GridY-1;gy++)for(int gx=0;gx<GridX-1;gx++){
                int bl=gy*GridX+gx,br=bl+1,tl=bl+GridX,tr=tl+1;
                triangles[n++]=bl;triangles[n++]=tl;triangles[n++]=br;triangles[n++]=tl;triangles[n++]=tr;triangles[n++]=br;
            }
        }
        static BoneWeight Blend(HumanPart part,int a,int b,int c,float w0,float w1,float w2)
        {
            var sum=new Dictionary<int,float>();
            void Add(int v,float w){for(int k=0;k<4;k++){int bone=part.skinIndex[v*4+k];float weight=part.skinWeight[v*4+k]*w;if(weight<=0)continue;sum.TryGetValue(bone,out float s);sum[bone]=s+weight;}}
            Add(a,w0);Add(b,w1);Add(c,w2);
            var list=new List<KeyValuePair<int,float>>(sum);list.Sort((p,q)=>q.Value.CompareTo(p.Value));
            float total=0;for(int k=0;k<list.Count&&k<4;k++)total+=list[k].Value;if(total<=0)return new BoneWeight{boneIndex0=part.skinIndex[a*4],weight0=1};
            var r=new BoneWeight();
            if(list.Count>0){r.boneIndex0=list[0].Key;r.weight0=list[0].Value/total;}
            if(list.Count>1){r.boneIndex1=list[1].Key;r.weight1=list[1].Value/total;}
            if(list.Count>2){r.boneIndex2=list[2].Key;r.weight2=list[2].Value/total;}
            if(list.Count>3){r.boneIndex3=list[3].Key;r.weight3=list[3].Value/total;}
            return r;
        }
    }
}
