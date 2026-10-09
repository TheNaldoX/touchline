using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Touchline
{
    public sealed class RenderBudget : MonoBehaviour
    {
        UniversalRenderPipelineAsset pipeline;
        float frame=.0167f,next;RenderTexture lastTarget;
        readonly MatchFrameHistory matchFrames=new MatchFrameHistory();
        public int MatchWidth {get;private set;}public int MatchHeight {get;private set;}public int MatchSpeed {get;private set;}
        public MatchFrameSummary MatchFrames=>matchFrames.Read();
        public void ResetMatchSample(){matchFrames.Reset();MatchWidth=MatchHeight=0;}
        public void RecordMatchFrame(float seconds,RenderTexture texture,int speed){if(MatchSpeed!=0&&MatchSpeed!=speed)matchFrames.Reset();matchFrames.Add(seconds);MatchSpeed=speed;if(texture!=null){MatchWidth=Mathf.RoundToInt(texture.width*Scale);MatchHeight=Mathf.RoundToInt(texture.height*Scale);}}
        public float FrameMilliseconds=>frame*1000;
        public float Scale=>pipeline==null?1:pipeline.renderScale;
        public void SetQuality(int mode){Application.targetFrameRate=mode==0?30:60;next=0;ResetMatchSample();if(pipeline!=null){pipeline.renderScale=Cap();pipeline.msaaSampleCount=MsaaSamples(mode);pipeline.mainLightShadowmapResolution=mode==0?512:2048;pipeline.shadowCascadeCount=mode==0?1:2;pipeline.shadowDistance=mode==0?40:65;}}
        void Awake(){var original=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(original==null)return;pipeline=Instantiate(original);GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;SetQuality(PlayerPrefs.GetInt("render-quality",1));}
        public static float ResolutionCap(int mode,int width,int height)=>Mathf.Clamp(Mathf.Sqrt(PixelBudget(mode)/Mathf.Max(1,(float)width*height)),.55f,1);
        // Pixel budgets and maximum texture dimensions, not UI logical units.
        static float PixelBudget(int mode)=>mode==2?2600000f:mode==0?1000000f:1600000f;
        public static int MsaaSamples(int mode)=>mode==2?4:2;
        public static Vector2Int ViewportSize(int mode,float width,float height,float density)
        {
            if(width<=0||height<=0||density<=0||float.IsNaN(width+height+density)||float.IsInfinity(width+height+density))return new Vector2Int(16,16);
            float maxWidth=mode==2?2160:1600,maxHeight=mode==2?2160:1000;
            float ratio=Mathf.Min(density,Mathf.Min(maxWidth/width,maxHeight/height));
            ratio=Mathf.Min(ratio,Mathf.Sqrt(PixelBudget(mode)/(width*height)));
            return new Vector2Int(Mathf.Max(16,Mathf.FloorToInt(width*ratio)),Mathf.Max(16,Mathf.FloorToInt(height*ratio)));
        }
        float Cap(){var camera=Camera.main;var texture=camera==null?null:camera.targetTexture;return ResolutionCap(PlayerPrefs.GetInt("render-quality",1),texture==null?Screen.width:texture.width,texture==null?Screen.height:texture.height);}
        void Update(){if(pipeline==null)return;var camera=Camera.main;var output=camera==null?null:camera.targetTexture;if(output!=lastTarget){lastTarget=output;pipeline.renderScale=Cap();next=Time.unscaledTime+2;}frame=Mathf.Lerp(frame,Mathf.Min(Time.unscaledDeltaTime,.1f),.04f);if(Time.unscaledTime<next)return;next=Time.unscaledTime+2;var cap=Cap();float target=Application.targetFrameRate==30?1f/30:1f/60;
            if(frame>=target*FastFrameRatio)fastSince=Time.unscaledTime;
            // Changer l'échelle réalloue les cibles de rendu (petit gel) : on ne la change que si c'est utile.
            float scale=NextScale(pipeline.renderScale,frame,target,cap,Time.unscaledTime-fastSince>=RaiseAfterSeconds);
            if(!Mathf.Approximately(scale,pipeline.renderScale)){pipeline.renderScale=scale;fastSince=Time.unscaledTime;}}
        const float SlowFrameRatio=1.3f,FastFrameRatio=1.08f; // durée d'image / cible : trop lent, assez rapide
        const float LowerStep=.05f,RaiseStep=.05f,MinimumScale=.55f;
        const float RaiseAfterSeconds=12f; // s d'images rapides sans interruption avant de remonter la résolution
        float fastSince;
        // Échelle de rendu suivante : baisse dès que les images sont trop lentes, remonte
        // seulement après une longue période rapide (pas d'oscillation, donc pas de gels répétés).
        public static float NextScale(float current,float frame,float target,float cap,bool fastLongEnough)
        {
            if(current>cap)return cap;
            if(frame>target*SlowFrameRatio)return Mathf.Max(MinimumScale,current-LowerStep);
            if(fastLongEnough&&frame<target*FastFrameRatio)return Mathf.Min(cap,current+RaiseStep);
            return current;
        }
        void OnDestroy(){if(pipeline!=null)Destroy(pipeline);}
    }
}
