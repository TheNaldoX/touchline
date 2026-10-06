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
        public void SetQuality(int mode){Application.targetFrameRate=mode==0?30:60;next=0;ResetMatchSample();if(pipeline!=null){pipeline.renderScale=Cap();pipeline.mainLightShadowmapResolution=mode==0?512:2048;pipeline.shadowCascadeCount=mode==0?1:2;pipeline.shadowDistance=mode==0?40:65;}}
        void Awake(){var original=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(original==null)return;pipeline=Instantiate(original);GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;SetQuality(PlayerPrefs.GetInt("render-quality",1));}
        public static float ResolutionCap(int mode,int width,int height)=>Mathf.Clamp(Mathf.Sqrt((mode==2?2600000f:mode==0?1000000f:1600000f)/Mathf.Max(1,(float)width*height)),.55f,1);
        float Cap(){var camera=Camera.main;var texture=camera==null?null:camera.targetTexture;return ResolutionCap(PlayerPrefs.GetInt("render-quality",1),texture==null?Screen.width:texture.width,texture==null?Screen.height:texture.height);}
        void Update(){if(pipeline==null)return;var camera=Camera.main;var output=camera==null?null:camera.targetTexture;if(output!=lastTarget){lastTarget=output;pipeline.renderScale=Cap();next=Time.unscaledTime+2;}frame=Mathf.Lerp(frame,Mathf.Min(Time.unscaledDeltaTime,.1f),.04f);if(Time.unscaledTime<next)return;next=Time.unscaledTime+2;var cap=Cap();float target=Application.targetFrameRate==30?1f/30:1f/60;pipeline.renderScale=Mathf.Clamp(pipeline.renderScale+(frame>target*1.3f?-.05f:frame<target*1.08f?.025f:0),.55f,cap);}
        void OnDestroy(){if(pipeline!=null)Destroy(pipeline);}
    }
}
