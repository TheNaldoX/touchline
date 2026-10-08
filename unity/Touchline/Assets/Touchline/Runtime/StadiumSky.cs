using UnityEngine;
using UnityEngine.Rendering;

namespace Touchline
{
    // Ciel au-dessus des tribunes : dégradé (zénith → horizon), nuages légers l'après-midi,
    // collines et silhouette de ville au loin, fenêtres allumées et halo orangé le soir.
    // Un dôme centré sur la caméra qui rend (aucune parallaxe, comme une skybox), dessiné en
    // premier sans écrire la profondeur : tout le stade passe devant, quelle que soit sa
    // distance. Rayon sous le début du brouillard : couleurs affichées telles quelles.
    // Coût : 1 appel de rendu, 1 matière (Unlit de Resources), 1 texture 512 × 256 sans mipmap,
    // générée une fois. Désactivé en qualité basse (RenderBudget mode 0) : fond uni comme avant.
    public sealed class StadiumSky : MonoBehaviour
    {
        public const float Radius=80f;                 // m, sous fogStartDistance (110 m au moins)
        public const float MinElevation=-4f,MaxElevation=90f; // ° couverts par la texture (bas → haut)
        public const int Width=512,Height=256;         // px : 512 px par demi-tour d'horizon
        public const int Repeats=2;                    // la texture fait deux fois le tour
        const int Segments=48,Rings=32;                // découpage du dôme
        const int BackgroundQueue=1000;                // file « Background » : avant tous les objets opaques
        const int CheckEveryFrames=30;                 // relecture de la préférence de qualité (≈ 0,5 s)

        // Élévation (°) d'une ligne de texture v (0 = bas, 1 = haut) : la résolution se
        // concentre près de l'horizon, où sont la silhouette et le dégradé le plus marqué.
        public static float ElevationAt(float v)=>MinElevation+(MaxElevation-MinElevation)*v*v;
        public static float RowAt(float elevation)=>Mathf.Sqrt(Mathf.Clamp01((elevation-MinElevation)/(MaxElevation-MinElevation)));
        public static bool Enabled(int quality)=>quality>BroadcastGrade.LowQuality;

        MeshRenderer view;int frames;

        public static StadiumSky Create(Transform parent,bool night,out Mesh mesh,out Texture2D texture,out Material material)
        {
            var go=new GameObject("Sky backdrop");go.transform.SetParent(parent,false);
            mesh=Dome();texture=Texture(night);
            material=StadiumLighting.UnlitMaterial(Color.white,texture);material.name="Sky backdrop";
            material.renderQueue=BackgroundQueue;material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",(float)CullMode.Off);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            var sky=go.AddComponent<StadiumSky>();sky.view=renderer;sky.Apply();return sky;
        }
        void OnEnable()=>RenderPipelineManager.beginCameraRendering+=Follow;
        void OnDisable()=>RenderPipelineManager.beginCameraRendering-=Follow;
        void Follow(ScriptableRenderContext context,Camera camera){if(camera!=null&&camera.cameraType==CameraType.Game)transform.position=camera.transform.position;}
        void Apply(){bool on=Enabled(PlayerPrefs.GetInt("render-quality",1));if(view!=null&&view.enabled!=on)view.enabled=on;}
        void LateUpdate(){if(++frames%CheckEveryFrames==0)Apply();}

        // Dôme vu de l'intérieur : anneaux d'élévation, u = tour d'horizon × Repeats, v = ligne de texture.
        public static Mesh Dome()
        {
            var vertices=new Vector3[(Rings+1)*(Segments+1)];var uv=new Vector2[vertices.Length];var triangles=new int[Rings*Segments*6];
            for(int r=0;r<=Rings;r++){
                float v=r/(float)Rings,e=ElevationAt(v)*Mathf.Deg2Rad;
                for(int s=0;s<=Segments;s++){
                    float u=s/(float)Segments,a=u*2*Mathf.PI;int i=r*(Segments+1)+s;
                    vertices[i]=new Vector3(Mathf.Cos(e)*Mathf.Sin(a),Mathf.Sin(e),Mathf.Cos(e)*Mathf.Cos(a))*Radius;uv[i]=new Vector2(u*Repeats,v);
                }
            }
            int t=0;
            for(int r=0;r<Rings;r++)for(int s=0;s<Segments;s++){
                int a=r*(Segments+1)+s,b=a+1,c=a+Segments+1,d=c+1;
                // Faces tournées vers le centre (vue de l'intérieur).
                triangles[t++]=a;triangles[t++]=d;triangles[t++]=b;triangles[t++]=a;triangles[t++]=c;triangles[t++]=d;
            }
            var mesh=new Mesh{name="Sky dome"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            // Le dôme suit la caméra : bornes larges pour ne jamais être écarté par le frustum.
            mesh.bounds=new Bounds(Vector3.zero,Vector3.one*(4*Radius));return mesh;
        }

        // Couleurs (sRGB). Après-midi : bleu profond au zénith, horizon voilé ; soir : nuit
        // d'encre, halo des lumières de la ville sur l'horizon.
        static readonly Color DayZenith=new Color(.27f,.44f,.70f),DayHorizon=new Color(.64f,.73f,.83f);
        static readonly Color DayFarHills=new Color(.52f,.61f,.70f),DayNearHills=new Color(.36f,.45f,.47f),DayCity=new Color(.42f,.48f,.54f);
        static readonly Color NightZenith=new Color(.008f,.012f,.03f),NightHorizon=new Color(.11f,.095f,.10f);
        static readonly Color NightFarHills=new Color(.045f,.045f,.06f),NightNearHills=new Color(.02f,.024f,.032f),NightCity=new Color(.026f,.028f,.04f);
        static readonly Color Window=new Color(.62f,.48f,.28f),Cloud=new Color(.95f,.96f,.98f);
        const float GradientElevation=35f;      // ° : au-delà, couleur du zénith
        const float HazeElevation=6f;           // ° : épaisseur du voile clair (jour) ou du halo (soir) sur l'horizon
        const float CloudLow=6f,CloudHigh=40f;  // ° : bande des nuages (après-midi)
        const float CloudOpacity=.35f;          // 0–1 au plus épais
        const float FarHillsTop=3.5f,NearHillsTop=.9f,CityTop=3f; // ° au plus haut
        const float WindowShare=.22f;           // part des fenêtres allumées le soir
        const float CityDistrict=.2f;           // seuil (−1…1) des quartiers d'immeubles : ≈ 43 % de l'horizon
        public static Color HorizonColor(bool night)=>night?NightHorizon:DayHorizon;
        public static Color ZenithColor(bool night)=>night?NightZenith:DayZenith;

        // Couleur du ciel nu (sans relief) à l'élévation e (°).
        public static Color SkyColor(bool night,float elevation,float x)
        {
            float t=Mathf.Clamp01(elevation/GradientElevation);
            var sky=Color.Lerp(HorizonColor(night),ZenithColor(night),Mathf.Pow(t,.6f));
            if(elevation<0)return HorizonColor(night);
            if(!night){
                // Nuages : bandes étirées à l'horizontale, plus fines et plus rares vers le zénith.
                if(elevation>CloudLow&&elevation<CloudHigh){
                    float band=Mathf.Sin(Mathf.PI*(elevation-CloudLow)/(CloudHigh-CloudLow));
                    float n=Noise(x,96,elevation/3.2f,7)*.65f+Noise(x,28,elevation/1.3f,11)*.35f;
                    float cover=Mathf.Clamp01((n-.55f)/.3f)*band*CloudOpacity;sky=Color.Lerp(sky,Cloud,cover);
                }
            }else{float glow=Mathf.Clamp01(1-elevation/HazeElevation);sky=Color.Lerp(sky,NightHorizon*1.25f,glow*glow*.5f);}
            return sky;
        }
        // Hauteur (°) d'une couche de relief à l'abscisse x (px, périodique sur Width).
        static float Ridge(float x,float top,int seed,float scale)=>top*(.2f+.8f*(Noise(x,scale,0,seed)*.7f+Noise(x,scale*.3f,0,seed+1)*.3f));
        // Immeubles : blocs de 6 à 14 px, hauteur aléatoire, seulement sur une partie de l'horizon.
        static float CityHeight(int x)
        {
            int block=0,start=0;uint h=97;
            while(true){h=h*1664525+1013904223;int w=6+(int)((h>>24)%9);if(x<start+w)break;start+=w;block++;}
            uint k=((uint)block+31)*2654435761u;k^=k>>15;
            // Deux quartiers d'immeubles par texture (quatre autour du stade), collines ailleurs.
            float district=Mathf.Sin(4*Mathf.PI*x/Width+.7f);if(district<CityDistrict)return 0;
            return CityTop*(.25f+.75f*((k>>8)&255)/255f)*Mathf.Clamp01((district-CityDistrict)/.15f);
        }
        // Relief d'une colonne de texture (°) : collines lointaines, proches, immeubles.
        public struct Skyline{public float far,near,city;}
        public static Skyline Column(int x)=>new Skyline{far=Ridge(x,FarHillsTop,3,70),near=Ridge(x,NearHillsTop,5,55),city=CityHeight(x)};
        public static Color Pixel(bool night,int x,int y)=>Pixel(night,x,y,Column(x));
        public static Color Pixel(bool night,int x,int y,Skyline line)
        {
            float elevation=ElevationAt((y+.5f)/Height);
            var color=SkyColor(night,elevation,x);
            float far=line.far,near=line.near,city=line.city;
            if(elevation<far)color=Color.Lerp(night?NightFarHills:DayFarHills,color,night?0:.15f);
            if(elevation<city){
                color=night?NightCity:DayCity;
                // Fenêtres : grille de 2 px × 1 ligne sur 3, allumées au hasard le soir.
                if(night&&x%2==0&&y%3==0){uint k=((uint)(x*73856093)^(uint)(y*19349663))*2654435761u;if(((k>>8)&1023)/1024f<WindowShare)color=Window*(.6f+.4f*((k>>20)&15)/15f);}
            }
            if(elevation<near)color=night?NightNearHills:DayNearHills;
            if(elevation<0)color=night?NightNearHills:DayNearHills; // sous l'horizon : sol lointain (caché par le stade)
            color.a=1;return color;
        }
        public static Texture2D Texture(bool night)
        {
            var texture=new Texture2D(Width,Height,TextureFormat.RGB24,false){name=night?"Night sky":"Afternoon sky",wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[Width*Height];
            var columns=new Skyline[Width];for(int x=0;x<Width;x++)columns[x]=Column(x);
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)pixels[y*Width+x]=Pixel(night,x,y,columns[x]);
            texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
        }
        // Bruit de valeur lissé, périodique sur Width px en x (raccord invisible du tour d'horizon) :
        // x en px, scale = px par maille (arrondi pour un nombre entier de mailles par tour).
        static float Noise(float x,float scale,float y,int seed)
        {
            int period=Mathf.Max(1,Mathf.RoundToInt(Width/scale));float lx=x/Width*period;
            int x0=Mathf.FloorToInt(lx),y0=Mathf.FloorToInt(y);float fx=lx-x0,fy=y-y0;
            fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
            int xa=((x0%period)+period)%period,xb=(xa+1)%period;
            float a=Lattice(xa,y0,seed),b=Lattice(xb,y0,seed),c=Lattice(xa,y0+1,seed),d=Lattice(xb,y0+1,seed);
            return Mathf.Lerp(Mathf.Lerp(a,b,fx),Mathf.Lerp(c,d,fx),fy);
        }
        static float Lattice(int x,int y,int seed){uint h=(uint)x*374761393u+(uint)y*668265263u+(uint)seed*2246822519u;h=(h^(h>>13))*1274126177u;h^=h>>16;return (h&65535)/65535f;}
    }
}
