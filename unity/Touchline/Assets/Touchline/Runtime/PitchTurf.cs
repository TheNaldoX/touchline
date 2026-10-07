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
