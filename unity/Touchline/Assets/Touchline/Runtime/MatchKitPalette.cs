using UnityEngine;

namespace Touchline
{
    // Generic change strip for readability, not a claim about an official away kit.
    public static class MatchKitPalette
    {
        public static Color Home(Color color)=>color.grayscale>.83f?new Color(.75f,.85f,.94f):color;
        public static Color Away(Color home,Color away)
        {
            away=Home(away);
            if(Vector3.Distance(new Vector3(home.r,home.g,home.b),new Vector3(away.r,away.g,away.b))>=.5f)return away;
            var light=new Color(.92f,.94f,.97f);var dark=new Color(.035f,.075f,.13f);
            return Contrast(home,light)>=Contrast(home,dark)?light:dark;
        }
        public static float Contrast(Color a,Color b)
        {
            float x=Luminance(a),y=Luminance(b);
            return (Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);
        }
        static float Luminance(Color c)=>.2126f*Mathf.GammaToLinearSpace(c.r)+.7152f*Mathf.GammaToLinearSpace(c.g)+.0722f*Mathf.GammaToLinearSpace(c.b);
    }
}
