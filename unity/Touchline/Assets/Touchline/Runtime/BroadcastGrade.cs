using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Touchline
{
    // Étalonnage « télé » léger : un seul passage de post-traitement URP (table de
    // couleurs 32³ + passe finale). Le soir, tonemapping neutre (les projecteurs ne
    // saturent plus en blanc) et un bloom discret sur les lampes et panneaux LED.
    // Désactivé en qualité basse (RenderBudget mode 0) : rendu identique à avant.
    public sealed class BroadcastGrade : MonoBehaviour
    {
        public const int LowQuality=0;           // mode « économie » de RenderBudget
        const string QualityKey="render-quality"; // même préférence que RenderBudget
        const int CheckEveryFrames=30;           // relecture de la préférence (≈ 0,5 s)
        // Jour : un peu plus de contraste et de saturation, blancs légèrement chauds.
        const float DayContrast=12,DaySaturation=10,DayTemperature=4; // échelles URP (−100…100)
        // Soir : tonemapping neutre compensé en exposition (IL), bloom au-dessus de 1,05 (HDR : lampes, LED).
        const float NightExposure=.25f,NightContrast=8,NightSaturation=6;
        const float BloomThreshold=1.05f,BloomIntensity=.35f,BloomScatter=.55f;

        Camera target;VolumeProfile profile;Volume volume;int frames;
        public bool Night{get;private set;}

        public static BroadcastGrade Attach(Transform parent,Camera camera,bool night)
        {
            var go=new GameObject("Broadcast grade");go.transform.SetParent(parent,false);
            var grade=go.AddComponent<BroadcastGrade>();grade.target=camera;grade.Night=night;
            grade.profile=Profile(night);grade.volume=go.AddComponent<Volume>();grade.volume.isGlobal=true;grade.volume.sharedProfile=grade.profile;
            grade.Apply();return grade;
        }
        public static bool Enabled(int quality)=>quality>LowQuality;

        public static VolumeProfile Profile(bool night)
        {
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name=night?"Broadcast grade · night":"Broadcast grade · day";
            var colour=profile.Add<ColorAdjustments>(true);
            colour.contrast.Override(night?NightContrast:DayContrast);colour.saturation.Override(night?NightSaturation:DaySaturation);
            if(night){
                profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);colour.postExposure.Override(NightExposure);
                var bloom=profile.Add<Bloom>(true);bloom.threshold.Override(BloomThreshold);bloom.intensity.Override(BloomIntensity);bloom.scatter.Override(BloomScatter);
                bloom.highQualityFiltering.Override(false);bloom.downscale.Override(BloomDownscaleMode.Quarter); // pyramide réduite : coût mobile limité
            }else profile.Add<WhiteBalance>(true).temperature.Override(DayTemperature);
            return profile;
        }

        void Apply()
        {
            bool on=Enabled(PlayerPrefs.GetInt(QualityKey,1));
            if(volume!=null&&volume.enabled!=on)volume.enabled=on;
            if(target!=null){var data=target.GetUniversalAdditionalCameraData();if(data.renderPostProcessing!=on)data.renderPostProcessing=on;}
        }
        void LateUpdate(){if(++frames%CheckEveryFrames==0)Apply();}
        void OnDestroy(){if(profile==null)return;if(Application.isPlaying)Destroy(profile);else DestroyImmediate(profile);}
    }
}
