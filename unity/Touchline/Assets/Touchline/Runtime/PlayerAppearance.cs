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
            var hair=materials[7];hair.mainTexture=hairTexture;hair.color=Color.Lerp(new Color(.20f,.16f,.13f),new Color(.85f,.70f,.50f),(identity%7)/6f);
            hair.SetFloat("_AlphaClip",1);hair.SetFloat("_Cutoff",.42f);hair.SetFloat("_Cull",0);hair.EnableKeyword("_ALPHATEST_ON");hair.SetOverrideTag("RenderType","TransparentCutout");hair.renderQueue=2450;
            materials[1].SetFloat("_Smoothness",.10f);materials[2].SetFloat("_Smoothness",.10f);
        }
    }
}
