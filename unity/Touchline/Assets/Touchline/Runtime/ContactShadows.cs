using UnityEngine;

namespace Touchline
{
    // Ombres de contact : une tache douce sous chaque joueur et sous le ballon, qui
    // pose les silhouettes sur la pelouse même au-delà de la distance des ombres
    // temps réel (40–65 m selon la qualité). Un seul maillage de quads mis à jour à
    // chaque image (92 sommets), une matière en multiplication (modèle
    // Rendering/WearMultiply, comme l'usure du gazon) : un appel de rendu.
    public sealed class ContactShadows
    {
        public const int TextureSize=64;            // pixels
        public const float Height=.03f;             // m au-dessus du gazon (au-dessus des lignes à 0,025 m)
        public const float PlayerRadius=.7f;        // m, demi-largeur de la tache sous un joueur
        public const float BallRadius=.2f;          // m, sous le ballon posé
        public const float Darkness=.55f;           // assombrissement au centre de la tache (0–1)
        public const float Core=.3f;                // part du rayon pleinement sombre (tache lisible de loin)
        public const float FadeHeight=1.6f;         // m : au-delà, plus de tache sous un objet en l'air
        const float BallSpread=1.5f;                // élargissement de la tache par mètre de hauteur du ballon
        const float BallRest=.11f;                  // m : hauteur du centre du ballon posé (son rayon)

        readonly Mesh mesh;readonly Vector3[] vertices;readonly int count;
        public Mesh Mesh=>mesh;
        public Vector3[] Vertices=>vertices;

        public ContactShadows(int casters)
        {
            count=casters;vertices=new Vector3[casters*4];
            var uv=new Vector2[casters*4];var triangles=new int[casters*6];
            for(int i=0;i<casters;i++){
                uv[i*4]=new Vector2(0,0);uv[i*4+1]=new Vector2(0,1);uv[i*4+2]=new Vector2(1,1);uv[i*4+3]=new Vector2(1,0);
                int v=i*4,t=i*6;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            mesh=new Mesh{name="Contact shadows"};mesh.MarkDynamic();mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(140,4,100)); // tout le terrain et ses abords
        }
        // Tache radiale : 1 (aucun effet en multiplication) au bord, 1 − Darkness dans le cœur.
        public static float Shade(float distanceFromCentre)=>1-Darkness*Mathf.SmoothStep(1,0,Mathf.InverseLerp(Core,1,distanceFromCentre));
        public static Texture2D Texture()
        {
            var texture=new Texture2D(TextureSize,TextureSize,TextureFormat.RGB24,true){name="Contact shadow",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[TextureSize*TextureSize];
            for(int y=0;y<TextureSize;y++)for(int x=0;x<TextureSize;x++){
                float dx=(x+.5f)/TextureSize*2-1,dy=(y+.5f)/TextureSize*2-1;float s=Shade(Mathf.Sqrt(dx*dx+dy*dy));pixels[y*TextureSize+x]=new Color(s,s,s,1);
            }
            texture.SetPixels32(pixels);texture.Apply(true,true);return texture;
        }
        // Taille relative (0–1) de la tache d'un objet dont le bas est à height m du sol :
        // la matière en multiplication ne s'atténue pas, la tache rétrécit donc en l'air.
        public static float Strength(float height)=>Mathf.Clamp01(1-height/FadeHeight);
        // Place la tache index sous position ; radius en m ; strength 0 la masque (quad réduit à un point).
        public void Set(int index,Vector3 position,float radius,float strength)
        {
            if(index<0||index>=count)return;int v=index*4;radius*=Mathf.Clamp01(strength);
            vertices[v]=new Vector3(position.x-radius,Height,position.z-radius);vertices[v+1]=new Vector3(position.x-radius,Height,position.z+radius);
            vertices[v+2]=new Vector3(position.x+radius,Height,position.z+radius);vertices[v+3]=new Vector3(position.x+radius,Height,position.z-radius);
        }
        public void SetBall(int index,Vector3 ball)
        {
            float lift=Mathf.Max(0,ball.y-BallRest);Set(index,ball,BallRadius*(1+lift*BallSpread),Strength(lift));
        }
        public void Apply()=>mesh.vertices=vertices;
    }
}
