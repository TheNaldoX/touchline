using UnityEngine;

namespace Touchline
{
    // Texture de gazon générée (aucun fichier image) : grain fin et touffes, sans
    // couture, répétée tous les GrainTile mètres. Elle module la couleur des bandes
    // de tonte au lieu d'un vert uni.
    public static class PitchTurf
    {
        public const int Size=256;           // pixels de côté
        public const float GrainTile=3.5f;   // m couverts par une répétition
        public const float GrainMin=.80f,GrainMax=1f; // luminosité relative du grain
        public const float MeanBrightness=(GrainMin+GrainMax)*.5f; // à compenser dans les couleurs

        // Luminosité (0–1) d'un point de la texture ; périodique de Size pixels.
        public static float Brightness(int x,int y)
        {
            // Trois octaves de bruit de valeur périodique : touffes (32 px), brins (8 px), grain (2 px).
            float n=Octave(x,y,8,1)*.5f+Octave(x,y,32,2)*.3f+Octave(x,y,128,3)*.2f;
            return Mathf.Lerp(GrainMin,GrainMax,n);
        }

        public static Texture2D Grain()
        {
            var texture=new Texture2D(Size,Size,TextureFormat.RGB24,true){name="Pitch turf grain",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color32[Size*Size];
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++){
                float v=Brightness(x,y);
                // Légère teinte : les brins clairs tirent vers le jaune, les creux vers le bleu.
                pixels[y*Size+x]=new Color(v*Mathf.Lerp(.96f,1f,v),v,v*Mathf.Lerp(1f,.94f,v));
            }
            texture.SetPixels32(pixels);texture.Apply(true,true);
            return texture;
        }

        // One RGB texture with mipmaps (~8 MiB): macro variation and fine blades
        // share the existing texture sample, with no extra material/render pass.
        public const int FieldWidth=2048,FieldHeight=1024;
        public static Color FieldPixel(float x,float z)
        {
            int gx=Mathf.RoundToInt(x/GrainTile*Size),gy=Mathf.RoundToInt(z/GrainTile*Size);
            float grain=Brightness(gx,gy);
            float broad=Mathf.PerlinNoise(19.3f+x*.16f,43.7f+z*.16f);
            float fine=Mathf.PerlinNoise(5.1f+x*.73f,17.2f+z*.73f);
            float tone=grain*Mathf.Lerp(.92f,1.06f,broad)*Mathf.Lerp(.97f,1.03f,fine);
            // Small warmer patches break the uniform green without looking muddy.
            return new Color(tone*Mathf.Lerp(.96f,1.015f,broad),tone,tone*Mathf.Lerp(.97f,.90f,broad),1);
        }
        public static Vector2 FieldUv(Vector3 point)=>new Vector2((point.x+HalfLength)/(2*HalfLength),(point.z+HalfWidth)/(2*HalfWidth));
        public static Texture2D FieldTexture()
        {
            var texture=new Texture2D(FieldWidth,FieldHeight,TextureFormat.RGB24,true){name="Full pitch turf",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color32[FieldWidth*FieldHeight];
            for(int y=0;y<FieldHeight;y++)for(int x=0;x<FieldWidth;x++)
                pixels[y*FieldWidth+x]=FieldPixel(((x+.5f)/FieldWidth*2-1)*HalfLength,((y+.5f)/FieldHeight*2-1)*HalfWidth);
            texture.SetPixels32(pixels);texture.Apply(true,true);return texture;
        }
        // Surface du terrain 105 × 68 m en bandes de tonte : sous-maillage 0 = bandes
        // claires, 1 = sombres. Le coin à l'ombre du toit (x < shadeEndX, z < shadeEdge)
        // forme un maillage à part (shaded) pour recevoir une matière assombrie.
        public const float HalfLength=52.5f,HalfWidth=34f,StripeWidth=10.5f; // m
        public static Mesh Surface(bool shaded,float shadeEdge=-HalfWidth,float shadeEndX=-HalfLength)
        {
            shadeEdge=Mathf.Clamp(shadeEdge,-HalfWidth,HalfWidth);shadeEndX=Mathf.Clamp(shadeEndX,-HalfLength,HalfLength);
            var vertices=new System.Collections.Generic.List<Vector3>();var stripes=new[]{new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>()};
            void Rect(int stripe,float x0,float x1,float z0,float z1)
            {
                if(x1-x0<.001f||z1-z0<.001f)return;int v=vertices.Count;
                vertices.Add(new Vector3(x0,0,z0));vertices.Add(new Vector3(x0,0,z1));vertices.Add(new Vector3(x1,0,z1));vertices.Add(new Vector3(x1,0,z0));
                stripes[stripe].AddRange(new[]{v,v+1,v+2,v,v+2,v+3});
            }
            int count=Mathf.RoundToInt(2*HalfLength/StripeWidth);
            for(int i=0;i<count;i++){
                float x0=-HalfLength+i*StripeWidth,x1=x0+StripeWidth;int stripe=i%2;
                if(shaded)Rect(stripe,x0,Mathf.Min(x1,shadeEndX),-HalfWidth,shadeEdge);
                else{Rect(stripe,x0,x1,shadeEdge,HalfWidth);Rect(stripe,Mathf.Max(x0,shadeEndX),x1,-HalfWidth,shadeEdge);}
            }
            var uv=new Vector2[vertices.Count];for(int i=0;i<uv.Length;i++)uv[i]=FieldUv(vertices[i]);
            var mesh=new Mesh{name=shaded?"Mowing surface in roof shadow":"Continuous mowing surface"};mesh.SetVertices(vertices);mesh.uv=uv;mesh.subMeshCount=2;mesh.SetTriangles(stripes[0],0);mesh.SetTriangles(stripes[1],1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }

        // Usure du gazon : zones piétinées devant les buts et au rond central, posées en
        // multiplication sur la pelouse (matière Rendering/WearMultiply, un seul appel de
        // rendu pour les trois zones). Blanc = gazon intact, beige = terre et herbe couchée.
        public const string WearMaterialPath="Rendering/WearMultiply";
        public const int WearSize=128;                    // pixels de côté
        public const float WearHeight=.012f;              // m au-dessus du gazon, sous les lignes (0,025 m)
        public const float GoalmouthDepth=7f,GoalmouthWidth=11f,CentreWear=7f; // m
        const float WearStrength=.75f;                    // part maximale de la teinte usée (centre de zone)
        static readonly Color Worn=new Color(.74f,.68f,.48f); // multiplicateur : herbe jaunie et terre
        static Texture2D wear;
        public static Texture2D WearTexture()
        {
            if(wear!=null)return wear;
            wear=new Texture2D(WearSize,WearSize,TextureFormat.RGB24,true){name="Pitch wear",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color32[WearSize*WearSize];
            for(int y=0;y<WearSize;y++)for(int x=0;x<WearSize;x++)pixels[y*WearSize+x]=Color.Lerp(Color.white,Worn,WearAmount(x,y));
            wear.SetPixels32(pixels);wear.Apply(true,true);return wear;
        }
        // Part d'usure (0–1) : forte au centre de la zone, bords irréguliers par le bruit.
        public static float WearAmount(int x,int y)
        {
            float u=(x+.5f)/WearSize*2-1,v=(y+.5f)/WearSize*2-1;float r=Mathf.Sqrt(u*u+v*v);
            float noise=Octave(x*2,y*2,16,7)*.6f+Octave(x*2,y*2,64,8)*.4f;
            float shape=1-Smooth(Mathf.Clamp01((r+(noise-.5f)*.45f-.25f)/.75f));
            float fade=1-Smooth(Mathf.Clamp01((r-.8f)/.2f)); // nul au bord du carré : pas de cadre visible
            return Mathf.Clamp01(shape*fade*WearStrength*Mathf.Lerp(.55f,1.15f,noise));
        }
        public static Mesh WearPatches()
        {
            var vertices=new System.Collections.Generic.List<Vector3>();var uv=new System.Collections.Generic.List<Vector2>();var triangles=new System.Collections.Generic.List<int>();
            void Patch(float x0,float x1,float z0,float z1,bool flip)
            {
                int v=vertices.Count;
                vertices.Add(new Vector3(x0,WearHeight,z0));vertices.Add(new Vector3(x0,WearHeight,z1));vertices.Add(new Vector3(x1,WearHeight,z1));vertices.Add(new Vector3(x1,WearHeight,z0));
                // Même texture, orientée autrement d'une zone à l'autre (taches différentes).
                uv.Add(flip?new Vector2(1,0):Vector2.zero);uv.Add(flip?new Vector2(1,1):Vector2.up);uv.Add(flip?new Vector2(0,1):Vector2.one);uv.Add(flip?Vector2.zero:Vector2.right);
                triangles.AddRange(new[]{v,v+1,v+2,v,v+2,v+3});
            }
            // Le centre de la zone devant le but est décalé vers le point de penalty (piétinement du gardien et des duels).
            Patch(-HalfLength,-HalfLength+GoalmouthDepth,-GoalmouthWidth*.5f,GoalmouthWidth*.5f,false);
            Patch(HalfLength-GoalmouthDepth,HalfLength,-GoalmouthWidth*.5f,GoalmouthWidth*.5f,true);
            Patch(-CentreWear*.5f,CentreWear*.5f,-CentreWear*.5f,CentreWear*.5f,true);
            var mesh=new Mesh{name="Pitch wear patches"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static Material WearMaterial()
        {
            var template=Resources.Load<Material>(WearMaterialPath);if(template==null)return null;
            var m=new Material(template){name="Pitch wear"};m.mainTexture=WearTexture();m.color=Color.white;return m;
        }

        // Bruit de valeur lissé, période = Size pixels (cells cellules sur la largeur).
        static float Octave(int x,int y,int cells,uint seed)
        {
            float cell=(float)Size/cells;float fx=x/cell,fy=y/cell;
            int x0=Mathf.FloorToInt(fx),y0=Mathf.FloorToInt(fy);float tx=Smooth(fx-x0),ty=Smooth(fy-y0);
            float Lattice(int i,int j){i=((i%cells)+cells)%cells;j=((j%cells)+cells)%cells;uint h=(uint)i*73856093u^(uint)j*19349663u^seed*83492791u;h^=h>>13;h*=1274126177u;h^=h>>16;return (h&65535)/65535f;}
            float a=Mathf.Lerp(Lattice(x0,y0),Lattice(x0+1,y0),tx),b=Mathf.Lerp(Lattice(x0,y0+1),Lattice(x0+1,y0+1),tx);
            return Mathf.Lerp(a,b,ty);
        }
        static float Smooth(float t)=>t*t*(3-2*t);
    }
}
