using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    // Texture de maillot générée en code, partagée par tous les joueurs d'une
    // équipe : le motif (rayures, cerceaux, écharpe…) est calculé à partir de la
    // position 3D (pose de repos, mètres) de chaque texel du maillot, donc continu
    // d'une pièce de tissu à l'autre malgré le découpage des UV.
    public static class KitTexture
    {
        public const int Width=512,Height=256;
        // Les UV du maillot MakeHuman occupent v ∈ [0,595 ; 0,98] : la texture ne
        // couvre que cette bande (meilleure résolution), recalée par _BaseMap_ST.
        public const float VMin=.58f;
        public static readonly Vector2 Scale=new Vector2(1,1/(1-VMin)),Offset=new Vector2(0,-VMin/(1-VMin));
        const float SleeveU=.70f;            // les manches sont les îlots UV au-delà de u = 0,70
        const float StripeWidth=.055f;       // m, largeur d'une rayure verticale
        const float HoopHeight=.075f;        // m, hauteur d'un cerceau
        const float SashHalfWidth=.05f;      // m, demi-largeur de l'écharpe diagonale
        const float SashSlope=.8f;           // m de décalage latéral par m de hauteur
        const float SashCentre=1.27f;        // m, hauteur où l'écharpe croise l'axe du corps
        const float BandHalfWidth=.06f;      // m, demi-largeur de la bande centrale
        const float NeckTrim=.013f,CuffTrim=.016f; // m, largeur du liseré au col et aux manches
        const float EdgeSoftness=.004f;      // m, adoucissement des bords de motif (≈ 1 texel)
        static readonly Vector3 CrestCentre=new Vector3(-.085f,1.39f,0); // m, écusson côté cœur (−x = gauche du joueur)
        const float CrestRadius=.026f,CrestInner=.015f; // m
        const int CacheSize=8;               // textures conservées (2 équipes + 2 gardiens par match)

        static Vector3[] positions;static byte[] regions; // 0 vide, 1 torse, 2 manche
        static float[] trimNeck,trimCuff;static HumanPart prepared;
        static readonly Dictionary<string,Texture2D> cache=new Dictionary<string,Texture2D>();
        static readonly List<string> order=new List<string>();

        public static Texture2D For(MatchKit kit,HumanPart shirt)
        {
            if(cache.TryGetValue(kit.key,out var cached)&&cached!=null){order.Remove(kit.key);order.Add(kit.key);return cached;}
            Prepare(shirt);
            var pixels=new Color32[Width*Height];var fallback=(Color32)kit.shirt;
            for(int i=0;i<pixels.Length;i++){
                if(regions[i]==0){pixels[i]=fallback;continue;}
                bool sleeve=regions[i]==2;var p=positions[i];
                float secondary=Secondary(kit.pattern,p,sleeve);
                var colour=Color.Lerp(kit.shirt,kit.trim,secondary);
                float neck=Mathf.Clamp01(.5f+(NeckTrim-trimNeck[i])/EdgeSoftness),cuff=Mathf.Clamp01(.5f+(CuffTrim-trimCuff[i])/EdgeSoftness);
                colour=Color.Lerp(colour,kit.trim,neck);
                colour=Color.Lerp(colour,kit.pattern==KitPattern.Sleeves?kit.shirt:kit.trim,cuff);
                if(!sleeve&&p.z>.02f){float r=new Vector2(p.x-CrestCentre.x,p.y-CrestCentre.y).magnitude;
                    colour=Color.Lerp(colour,kit.trim,Mathf.Clamp01(.5f+(CrestRadius-r)/EdgeSoftness)*(1-Mathf.Clamp01(.5f+(CrestInner-r)/EdgeSoftness)));}
                colour.a=1;pixels[i]=colour;
            }
            var texture=new Texture2D(Width,Height,TextureFormat.RGB24,true,false){name="Kit "+kit.key,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
            texture.SetPixels32(pixels);texture.Apply(true,true);
            cache[kit.key]=texture;order.Remove(kit.key);order.Add(kit.key);
            while(order.Count>CacheSize){var old=order[0];order.RemoveAt(0);if(cache.TryGetValue(old,out var t)&&t!=null){if(Application.isPlaying)Object.Destroy(t);else Object.DestroyImmediate(t);}cache.Remove(old);}
            return texture;
        }

        // Part de couleur secondaire (0–1) au point p du maillot (pose de repos, m).
        public static float Secondary(KitPattern pattern,Vector3 p,bool sleeve)
        {
            switch(pattern){
                case KitPattern.Stripes:return sleeve?0:Wave(p.x/StripeWidth+.5f,StripeWidth);
                case KitPattern.Hoops:return Wave((p.y-1f)/HoopHeight,HoopHeight);
                case KitPattern.Sleeves:return sleeve?1:0;
                case KitPattern.Sash:if(sleeve)return 0;return Mathf.Clamp01(.5f+(SashHalfWidth-Mathf.Abs(p.x-(p.y-SashCentre)*SashSlope)/Mathf.Sqrt(1+SashSlope*SashSlope))/EdgeSoftness);
                case KitPattern.CentreBand:return sleeve?0:Mathf.Clamp01(.5f+(BandHalfWidth-Mathf.Abs(p.x))/EdgeSoftness);
                default:return 0;
            }
        }
        // Bandes alternées : 1 sur les bandes impaires, transition douce sur EdgeSoftness.
        static float Wave(float s,float period)=>Mathf.Clamp01(.5f-Mathf.Sin(Mathf.PI*s)*period/(Mathf.PI*EdgeSoftness));

        static void Prepare(HumanPart shirt)
        {
            if(prepared==shirt&&positions!=null)return;
            prepared=shirt;int n=Width*Height;positions=new Vector3[n];regions=new byte[n];trimNeck=new float[n];trimCuff=new float[n];
            int count=shirt.position.Length/3;var P=new Vector3[count];var U=new Vector2[count];
            for(int v=0;v<count;v++){P[v]=new Vector3(shirt.position[v*3],shirt.position[v*3+1],shirt.position[v*3+2]);U[v]=new Vector2(shirt.uv[v*2]*Width-.5f,(shirt.uv[v*2+1]-VMin)/(1-VMin)*Height-.5f);}
            var index=shirt.index;
            for(int t=0;t<index.Length;t+=3){
                int a=index[t],b=index[t+1],c=index[t+2];var ua=U[a];var ub=U[b];var uc=U[c];
                byte region=(byte)((shirt.uv[a*2]+shirt.uv[b*2]+shirt.uv[c*2])/3>SleeveU?2:1);
                float area=(ub.x-ua.x)*(uc.y-ua.y)-(uc.x-ua.x)*(ub.y-ua.y);if(Mathf.Abs(area)<1e-9f)continue;
                int x0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(ua.x,Mathf.Min(ub.x,uc.x)))),x1=Mathf.Min(Width-1,Mathf.CeilToInt(Mathf.Max(ua.x,Mathf.Max(ub.x,uc.x))));
                int y0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(ua.y,Mathf.Min(ub.y,uc.y)))),y1=Mathf.Min(Height-1,Mathf.CeilToInt(Mathf.Max(ua.y,Mathf.Max(ub.y,uc.y))));
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){
                    float w1=((x-ua.x)*(uc.y-ua.y)-(uc.x-ua.x)*(y-ua.y))/area,w2=((ub.x-ua.x)*(y-ua.y)-(x-ua.x)*(ub.y-ua.y))/area,w0=1-w1-w2;
                    if(w0<-.02f||w1<-.02f||w2<-.02f)continue;
                    int i=y*Width+x;positions[i]=P[a]*w0+P[b]*w1+P[c]*w2;regions[i]=region;
                }
            }
            // Dilatation : quelques texels autour des îlots évitent les coutures sombres au filtrage.
            for(int pass=0;pass<4;pass++){var snapshot=(byte[])regions.Clone();
                for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){int i=y*Width+x;if(snapshot[i]!=0)continue;
                    for(int k=0;k<4;k++){int nx=x+(k==0?1:k==1?-1:0),ny=y+(k==2?1:k==3?-1:0);if(nx<0||ny<0||nx>=Width||ny>=Height)continue;int j=ny*Width+nx;if(snapshot[j]!=0){positions[i]=positions[j];regions[i]=snapshot[j];break;}}}}
            // Bords libres du maillot : col (près de l'axe, en haut) et poignets (bout des manches).
            var edges=new Dictionary<long,int>();
            for(int t=0;t<index.Length;t+=3)for(int k=0;k<3;k++){int a=index[t+k],b=index[t+(k+1)%3];long key=a<b?((long)a<<32)|(uint)b:((long)b<<32)|(uint)a;edges.TryGetValue(key,out int used);edges[key]=used+1;}
            var neck=new List<Vector3>();var cuff=new List<Vector3>();
            foreach(var e in edges){if(e.Value!=1)continue;var a=P[(int)(e.Key>>32)];var b=P[(int)(e.Key&0xffffffff)];
                if(Mathf.Abs(a.x)<.16f&&Mathf.Abs(b.x)<.16f&&a.y>1.46f&&b.y>1.46f){neck.Add(a);neck.Add(b);}
                else if(Mathf.Abs(a.x)>.29f&&Mathf.Abs(b.x)>.29f){cuff.Add(a);cuff.Add(b);}}
            for(int i=0;i<n;i++){var p=positions[i];trimNeck[i]=trimCuff[i]=1;if(regions[i]==0)continue;
                if(p.y>1.40f&&Mathf.Abs(p.x)<.22f)trimNeck[i]=Nearest(p,neck);
                if(Mathf.Abs(p.x)>.26f)trimCuff[i]=Nearest(p,cuff);}
        }
        static float Nearest(Vector3 p,List<Vector3> segments)
        {
            float best=1;
            for(int s=0;s<segments.Count;s+=2){var a=segments[s];var ab=segments[s+1]-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(1e-8f,ab.sqrMagnitude));best=Mathf.Min(best,(a+ab*t-p).magnitude);}
            return best;
        }
    }
}
