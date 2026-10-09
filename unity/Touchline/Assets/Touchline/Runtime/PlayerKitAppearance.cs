using Touchline.Core;
using UnityEngine;

namespace Touchline
{
    // Tenue du joueur : texture de maillot partagée par l'équipe (KitTexture),
    // short et chaussettes, flocage du dos (ShirtPrint). Le gardien porte la
    // tenue de gardien de son équipe ; un remplaçant reprend le flocage du nouveau joueur.
    public sealed partial class PlayerView
    {
        const int PrintSlot=8; // index du matériau de flocage dans ownedMaterials
        const int ShirtMaterial=1; // matériau « maillot » du modèle MakeHuman
        MatchKit kit;int printCell=-1;string printName="";int printNumber;
        public MatchKit Kit=>kit;
        public int PrintCell=>printCell;
        static HumanPart ShirtPart{get{foreach(var part in source.parts)if(part.material==ShirtMaterial)return part;return source.parts[0];}}

        static Material PrintMaterial()
        {
            var m=Material(Color.white);m.mainTexture=ShirtPrint.Atlas;m.mainTextureScale=ShirtPrint.CellScale;m.SetFloat("_Smoothness",.22f);
            m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.5f);m.EnableKeyword("_ALPHATEST_ON");m.SetFloat("_AlphaToMask",1);
            m.SetOverrideTag("RenderType","TransparentCutout");m.renderQueue=2450;
            m.SetShaderPassEnabled("ShadowCaster",false); // le flocage ne projette pas d'ombre (pas de passe en plus)
            return m;
        }
        MatchKit WornKit=>keeperAppearance&&kit.keeper!=null?kit.keeper:kit;
        void ApplyKit()
        {
            var worn=WornKit;bool textured=worn.Textured;
            // Matte fabric distinguishes cloth from skin and boot leather.
            shirt.SetFloat("_Smoothness",.08f);ownedMaterials[2].SetFloat("_Smoothness",.08f);ownedMaterials[3].SetFloat("_Smoothness",.04f);
            shirt.mainTexture=textured?KitTexture.For(worn,ShirtPart):null;shirt.color=textured?Color.white:worn.shirt;
            shirt.mainTextureScale=textured?KitTexture.Scale:Vector2.one;shirt.mainTextureOffset=textured?KitTexture.Offset:Vector2.zero;
            ownedMaterials[2].color=worn.shorts;ownedMaterials[3].color=worn.socks;
            UpdatePrint();
        }
        void SetPrintIdentity(PlayerData data)
        {
            printName=ShirtLettering.Printable(!string.IsNullOrEmpty(data.surname)?data.surname:ShirtLettering.Surname(data.name));printNumber=data.number;
            if(kit!=null&&ownedMaterials!=null)UpdatePrint();
        }
        void UpdatePrint()
        {
            var worn=WornKit;ReleasePrint();
            printCell=ShirtPrint.Acquire(printName,printNumber,worn.printFill,worn.printOutline);
            var m=ownedMaterials[PrintSlot];m.mainTexture=ShirtPrint.Atlas;m.color=printCell<0?Color.clear:Color.white;
            m.mainTextureOffset=ShirtPrint.CellOffset(Mathf.Max(0,printCell));
        }
        void ReleasePrint(){ShirtPrint.Release(printCell);printCell=-1;}
    }
}
