using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    // Android's reported DPI varies with display zoom and fold state. Size the
    // interface from the active viewport, independently of the 3D render scale.
    public static class InterfaceViewport
    {
        public static float Scale(int width,int height,int size=1)
        {
            float aspect=width/(float)Mathf.Max(1,height);
            float logicalWidth=aspect<.7f?440:aspect<1.55f?1120:1280;
            float preference=size==0?.9f:size==2?1.12f:1;
            return Mathf.Clamp(width/logicalWidth*preference,.45f,5);
        }
        public static void Apply(PanelSettings panel,int width,int height,int size=1)
        {
            panel.scaleMode=PanelScaleMode.ConstantPixelSize;
            panel.scale=Scale(width,height,size);
        }
    }
}
