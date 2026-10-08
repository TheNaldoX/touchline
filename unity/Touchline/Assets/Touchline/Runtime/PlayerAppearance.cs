using UnityEngine;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        static Texture2D[] skinTextures;static Texture2D eyeTexture,hairTexture;
        static uint AppearanceIdentity(string id){uint hash=2166136261;foreach(char c in id??"player")hash=unchecked((hash^c)*16777619);return hash;}
        static void ApplyAppearance(Material[] materials,uint identity)
        {
            if(skinTextures==null){
                skinTextures=new[]{Resources.Load<Texture2D>("Models/skin-light"),Resources.Load<Texture2D>("Models/skin-medium"),Resources.Load<Texture2D>("Models/skin-dark")};
                eyeTexture=Resources.Load<Texture2D>("Models/eyes-brown");
                hairTexture=Resources.Load<Texture2D>("Models/hair-short");
            }
            // Generic, stable visual variants; these are not scans or verified
            // physical likenesses of the real players in the database.
            var texture=skinTextures[identity%skinTextures.Length];
            if(texture!=null){materials[0].mainTexture=texture;materials[0].color=Color.white*Mathf.Lerp(.88f,1,(identity%23)/22f);materials[0].SetFloat("_Smoothness",.24f);}
            materials[6].mainTexture=eyeTexture;materials[6].SetFloat("_Smoothness",.52f);
            var hair=materials[7];hair.mainTexture=hairTexture;hair.color=HairColour(identity);
            hair.SetFloat("_AlphaClip",1);hair.SetFloat("_Cutoff",.42f);hair.SetFloat("_Cull",0);hair.EnableKeyword("_ALPHATEST_ON");hair.SetOverrideTag("RenderType","TransparentCutout");hair.renderQueue=2450;
            materials[1].SetFloat("_Smoothness",.10f);materials[2].SetFloat("_Smoothness",.10f);
            materials[5].color=BootColour(identity);materials[5].SetFloat("_Smoothness",.45f); // chaussures vernies
        }
        // Couleurs de cheveux plausibles selon la teinte de peau (identity % 3 : clair, moyen, foncé).
        // Environ 1 joueur sur 16 a le crâne rasé (cheveux transparents, coupés par l'alpha).
        static readonly Color[] HairShades={
            new Color(.035f,.03f,.028f),new Color(.11f,.075f,.05f),new Color(.24f,.15f,.085f),new Color(.40f,.28f,.16f),
            new Color(.60f,.46f,.28f),new Color(.78f,.64f,.40f),new Color(.52f,.24f,.10f),new Color(.88f,.84f,.72f)};
        static readonly int[][] HairWeights={new[]{2,4,4,3,2,2,1,0},new[]{6,5,2,1,0,0,0,1},new[]{10,2,0,0,0,0,0,1}};
        const int ShavedOneIn=16;
        public static Color HairColour(uint identity)
        {
            if((identity>>7)%ShavedOneIn==0)return new Color(0,0,0,0);
            var weights=HairWeights[identity%3];int total=0;foreach(int w in weights)total+=w;
            int pick=(int)((identity>>11)%(uint)total);
            for(int i=0;i<weights.Length;i++){if(pick<weights[i])return HairShades[i];pick-=weights[i];}
            return HairShades[0];
        }
        // Chaussures : noir le plus souvent, sinon blanc ou couleurs vives (fixe pour un joueur).
        static readonly Color[] BootShades={
            new Color(.045f,.055f,.065f),new Color(.045f,.055f,.065f),new Color(.045f,.055f,.065f),new Color(.92f,.93f,.94f),new Color(.92f,.93f,.94f),
            new Color(.98f,.42f,.08f),new Color(.80f,.95f,.15f),new Color(.10f,.40f,.95f),new Color(.88f,.10f,.14f),new Color(.95f,.40f,.65f),new Color(.80f,.68f,.30f),new Color(.12f,.85f,.80f)};
        // Coupes : la coque de cheveux MakeHuman est déformée dans deux copies partagées du
        // maillage (volume sur le dessus, ou houppe à l'avant) ; aucun coût de rendu en plus.
        const int HairMaterialIndex=7,FullHair=1,Quiff=2;
        static int hairStart,hairCount;static Mesh[] hairStyles;SkinnedMeshRenderer bodyRenderer;
        static readonly Vector3 HairCentre=new Vector3(0,1.70f,.03f); // m, centre approximatif du crâne (pose de repos)
        const float FullHairSwell=.16f;  // agrandissement relatif de la coque sur le dessus (≈ 1,6 cm)
        const float QuiffLift=.025f,QuiffForward=.012f; // m
        public static int HairStyle(uint identity){uint style=(identity>>21)%8;return style==7?Quiff:style>=5?FullHair:0;}
        Mesh AnatomyMesh(uint identity)
        {
            if(hairStyles==null||hairStyles[0]!=geometry)hairStyles=new[]{geometry,Restyle(geometry,FullHair),Restyle(geometry,Quiff)};
            return hairStyles[HairStyle(identity)];
        }
        static Mesh Restyle(Mesh source,int style)
        {
            var mesh=Object.Instantiate(source);mesh.name=source.name+(style==FullHair?" · full hair":" · quiff");var vertices=mesh.vertices;
            for(int i=hairStart;i<hairStart+hairCount;i++){var p=vertices[i];
                if(style==FullHair)vertices[i]=HairCentre+(p-HairCentre)*(1+FullHairSwell*Mathf.SmoothStep(0,1,(p.y-1.66f)/.14f));
                else{float w=Mathf.SmoothStep(0,1,(p.y-1.76f)/.06f)*Mathf.SmoothStep(0,1,p.z/.12f);vertices[i]=p+new Vector3(0,QuiffLift,QuiffForward)*w;}}
            mesh.vertices=vertices;mesh.RecalculateBounds();return mesh;
        }
        public static Color BootColour(uint identity)=>BootShades[(int)((identity>>17)%(uint)BootShades.Length)];
    }
}
