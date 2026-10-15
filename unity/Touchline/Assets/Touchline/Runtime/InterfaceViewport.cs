using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    // Android's reported DPI varies with display zoom and fold state. Size the
    // interface from the active viewport, independently of the 3D render scale.
    public static class InterfaceViewport
    {
        // Largeur logique (unités UI) visée selon la forme de l'écran. Sur le Galaxy Z Fold
        // (≈ 2,625 px physiques par dp), 412 unités sur l'écran plié (1080 px) et 832 sur
        // l'écran déplié (2184 px) donnent ≈ 1 unité = 1 dp sur les deux écrans : les tailles de la
        // feuille de style (48 px = cible tactile, 12 px = plus petit texte) sont les tailles réelles
        // au doigt. Avec 860, l'écran déplié rendait 0,97 dp : boutons 46 dp, textes 11,6 sp.
        public const float PortraitLogicalWidth=412,SquareLogicalWidth=832,LandscapeLogicalWidth=1280;
        // Largeur logique minimale de la mise en page « bureau » (menu latéral). En dessous,
        // l'interface passe en mode compact (onglets en bas). Doit rester sous SquareLogicalWidth
        // pour que l'écran déplié garde le menu latéral.
        public const float WideLayoutMinWidth=820,WideLayoutMinHeight=680;
        public static float Scale(int width,int height,int size=1)
        {
            float aspect=width/(float)Mathf.Max(1,height);
            float logicalWidth=aspect<.7f?PortraitLogicalWidth:aspect<1.55f?SquareLogicalWidth:LandscapeLogicalWidth;
            float preference=size==0?.9f:size==2?1.12f:1;
            return Mathf.Clamp(width/logicalWidth*preference,.45f,5);
        }
        public static bool WideLayout(float width,float height)=>width>=WideLayoutMinWidth&&height>=WideLayoutMinHeight;
        public static void Apply(PanelSettings panel,int width,int height,int size=1)
        {
            panel.scaleMode=PanelScaleMode.ConstantPixelSize;
            panel.scale=Scale(width,height,size);
        }
    }
}
