using UnityEngine;

namespace Touchline
{
    public enum KitPattern { Plain, Stripes, Hoops, Sleeves, Sash, CentreBand }

    // Tenue de match générique aux couleurs du club : motif choisi de façon
    // déterministe à partir de l'identifiant du club (pas un maillot officiel).
    public sealed class MatchKit
    {
        public KitPattern pattern;
        public Color shirt,trim,shorts,socks,printFill,printOutline;
        public MatchKit keeper; // tenue du gardien de la même équipe (null pour une tenue de gardien)
        public string key;      // identifie la texture de maillot partagée
        public bool Textured=>pattern!=KitPattern.Plain||trim!=shirt;

        // Couleurs secondaires possibles (sRVB) ; blanc, marine et noir comptent double.
        static readonly Color[] Accents={
            new Color(.94f,.95f,.96f),new Color(.94f,.95f,.96f),new Color(.05f,.09f,.22f),new Color(.05f,.09f,.22f),new Color(.06f,.06f,.07f),
            new Color(.80f,.08f,.10f),new Color(.95f,.75f,.15f),new Color(.45f,.70f,.92f),new Color(.05f,.45f,.25f),new Color(.45f,.08f,.18f)};
        // Tenues de gardien : jaune, vert vif, orange, rose, cyan, violet, noir, gris.
        static readonly Color[] KeeperColours={
            new Color(.95f,.80f,.12f),new Color(.55f,.85f,.15f),new Color(.95f,.45f,.08f),new Color(.92f,.35f,.62f),
            new Color(.15f,.75f,.85f),new Color(.45f,.25f,.70f),new Color(.08f,.08f,.09f),new Color(.55f,.57f,.60f)};
        const float MinimumAccentContrast=1.8f;  // rapport de luminance (WCAG) entre couleur principale et secondaire
        const float MinimumKeeperDistance=.45f;  // distance RVB minimale entre un gardien et les maillots de champ

        public static uint Hash(string text){uint hash=2166136261;foreach(char c in text??"club")hash=unchecked((hash^c)*16777619);return hash;}

        // Tenue unie (comportement historique) : maillot, short et chaussettes dans la couleur d'équipe.
        public static MatchKit Plain(Color team)
        {
            var kit=new MatchKit{pattern=KitPattern.Plain,shirt=team,trim=team,shorts=team*.58f,socks=team*.8f};
            kit.shorts.a=kit.socks.a=1;Prints(kit);kit.key="plain";
            kit.keeper=Keeper(new Color(.85f,.65f,.12f),1);return kit;
        }

        // Tenues des deux équipes d'un match. awayClub : couleur d'origine du club visiteur
        // (différente d'awayStrip quand MatchKitPalette impose une tenue de rechange).
        public static MatchKit[] ForMatch(string homeId,Color homeStrip,string awayId,Color awayStrip,Color awayClub)
        {
            var home=Design(homeId,homeStrip,null);
            bool change=Distance(MatchKitPalette.Home(awayClub),awayStrip)>.01f;
            var away=Design(awayId,awayStrip,change?MatchKitPalette.Home(awayClub):(Color?)null);
            // Gardiens : couleur éloignée des deux maillots de champ et de l'autre gardien.
            home.keeper=Keeper(PickKeeper(Hash(homeId),home,away,null),Hash(homeId));
            away.keeper=Keeper(PickKeeper(Hash(awayId)>>3,home,away,home.keeper.shirt),Hash(awayId));
            return new[]{home,away};
        }

        static MatchKit Design(string clubId,Color primary,Color? changeAccent)
        {
            uint hash=Hash(clubId);var kit=new MatchKit{shirt=primary};
            if(changeAccent.HasValue){
                // Tenue de rechange : unie ou manches contrastées, rappel de la couleur du club.
                kit.pattern=hash%2==0?KitPattern.Plain:KitPattern.Sleeves;
                kit.trim=MatchKitPalette.Contrast(primary,changeAccent.Value)>=1.4f?changeAccent.Value:Accent(primary,hash);
            }else{
                kit.pattern=(KitPattern)(hash%6);
                kit.trim=Accent(primary,hash>>4);
            }
            kit.shorts=(hash>>9)%3==1?kit.trim:(hash>>9)%3==2?Color.Lerp(primary,Color.black,.15f):primary;
            kit.socks=(hash>>12)%2==0?kit.shorts:primary;
            Prints(kit);kit.key=clubId+"|"+ColorUtility.ToHtmlStringRGB(primary)+"|"+ColorUtility.ToHtmlStringRGB(kit.trim)+"|"+kit.pattern;
            return kit;
        }

        static Color Accent(Color primary,uint hash)
        {
            int count=0;foreach(var c in Accents)if(Usable(primary,c))count++;
            if(count==0)return MatchKitPalette.Contrast(primary,Color.white)>MatchKitPalette.Contrast(primary,Color.black)?Accents[0]:Accents[4];
            int pick=(int)(hash%(uint)count);
            foreach(var c in Accents)if(Usable(primary,c)&&pick--==0)return c;
            return Accents[0];
        }
        static bool Usable(Color primary,Color accent)=>MatchKitPalette.Contrast(primary,accent)>=MinimumAccentContrast&&Distance(primary,accent)>.45f;

        static Color PickKeeper(uint hash,MatchKit home,MatchKit away,Color? otherKeeper)
        {
            int start=(int)(hash%(uint)KeeperColours.Length);Color best=KeeperColours[start];float bestScore=-1;
            for(int i=0;i<KeeperColours.Length;i++){
                var c=KeeperColours[(start+i)%KeeperColours.Length];
                float score=Mathf.Min(Mathf.Min(Distance(c,home.shirt),Distance(c,away.shirt)),Mathf.Min(Distance(c,home.trim),Distance(c,away.trim)));
                if(otherKeeper.HasValue)score=Mathf.Min(score,Distance(c,otherKeeper.Value));
                if(score>=MinimumKeeperDistance)return c;
                if(score>bestScore){bestScore=score;best=c;}
            }
            return best;
        }

        static MatchKit Keeper(Color primary,uint hash)
        {
            var dark=new Color(.06f,.06f,.07f);
            var trim=Luminance(primary)>.18f?Color.Lerp(primary,dark,.7f):Color.Lerp(primary,Color.white,.55f);
            var kit=new MatchKit{pattern=hash%3==0?KitPattern.Sleeves:KitPattern.Plain,shirt=primary,trim=trim};
            kit.shorts=(hash>>5)%2==0?primary:trim;kit.socks=primary;Prints(kit);
            kit.key="keeper|"+ColorUtility.ToHtmlStringRGB(primary)+"|"+kit.pattern;return kit;
        }

        // Flocage lisible : la couleur secondaire si elle contraste assez, sinon blanc ou noir.
        static void Prints(MatchKit kit)
        {
            var light=new Color(.97f,.97f,.97f);var dark=new Color(.05f,.06f,.08f);
            // Le numéro est posé sur la bande centrale (CentreBand) ou sur la couleur principale ;
            // rayures et cerceaux mélangent les deux couleurs derrière lui.
            bool band=kit.pattern==KitPattern.CentreBand,mixed=kit.pattern==KitPattern.Stripes||kit.pattern==KitPattern.Hoops;
            var behind=band?kit.trim:kit.shirt;var other=band?kit.shirt:kit.trim;
            if(!mixed&&MatchKitPalette.Contrast(behind,other)>=3f)kit.printFill=other;
            else{
                float l=Mathf.Min(MatchKitPalette.Contrast(behind,light),mixed?MatchKitPalette.Contrast(other,light):99);
                float d=Mathf.Min(MatchKitPalette.Contrast(behind,dark),mixed?MatchKitPalette.Contrast(other,dark):99);
                kit.printFill=l>=d?light:dark;
            }
            bool lightFill=Luminance(kit.printFill)>.4f;
            kit.printOutline=mixed?(lightFill?dark:light):Color.Lerp(behind,lightFill?dark:light,.5f);
            kit.printFill.a=kit.printOutline.a=1;
        }

        public static float Distance(Color a,Color b)=>new Vector3(a.r-b.r,a.g-b.g,a.b-b.b).magnitude;
        static float Luminance(Color c)=>.2126f*c.r+.7152f*c.g+.0722f*c.b;
    }
}
