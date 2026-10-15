using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    // The broadcast is part of the UI composition, above its opaque background.
    // Never depend on a transparent hole through every ancestor of the panel.
    public sealed class MatchViewport : MonoBehaviour
    {
        MatchArena arena;Image surface;RenderTexture texture;readonly MatchTouchGesture gesture=new MatchTouchGesture();
        MatchPlayerLabels names;int requestedMsaa;
        MatchBallLocator locator;
        MatchRestartCut restartCut;
        public MatchPlayerLabels PlayerLabels=>names;
        public MatchBallLocator BallLocator=>locator;
        public RenderTexture Texture=>texture;
        public bool Bound=>surface!=null&&surface.panel!=null;
        public void Bind(MatchArena match,Image image)
        {
            if(surface!=null){surface.UnregisterCallback<PointerDownEvent>(Press);surface.UnregisterCallback<PointerMoveEvent>(Move);surface.UnregisterCallback<PointerUpEvent>(Lift);surface.UnregisterCallback<PointerCancelEvent>(Cancel);surface.UnregisterCallback<PointerCaptureOutEvent>(Lost);}
            arena=match;surface=image;gesture.Reset();surface.image=texture;
            // UI Toolkit elements must be created on the Unity main thread,
            // after construction of the MonoBehaviour.
            names??=new MatchPlayerLabels();names.Bind(match,image);
            locator??=new MatchBallLocator();locator.Bind(match,image);
            restartCut??=new MatchRestartCut();restartCut.Bind(match,image);
            surface.RegisterCallback<PointerDownEvent>(Press);surface.RegisterCallback<PointerMoveEvent>(Move);surface.RegisterCallback<PointerUpEvent>(Lift);surface.RegisterCallback<PointerCancelEvent>(Cancel);surface.RegisterCallback<PointerCaptureOutEvent>(Lost);
        }
        void Press(PointerDownEvent e){if(e.button!=0)return;gesture.Down(e.pointerId,e.position);surface.CapturePointer(e.pointerId);e.StopPropagation();}
        void Move(PointerMoveEvent e){float scale=gesture.Move(e.pointerId,e.position);if(scale!=1)arena.ScaleZoom(scale);}
        void Cancel(PointerCancelEvent e){gesture.Cancel(e.pointerId);surface.ReleasePointer(e.pointerId);}
        void Lost(PointerCaptureOutEvent e)=>gesture.Cancel(e.pointerId);
        void Lift(PointerUpEvent e){bool tap=gesture.Up(e.pointerId,e.position);surface.ReleasePointer(e.pointerId);if(!tap)return;var local=surface.WorldToLocal(e.position);SelectAt(new Vector2(local.x/surface.resolvedStyle.width,1-local.y/surface.resolvedStyle.height));}
        public string SelectAt(Vector2 uv)
        {
            string id=FindPlayerAt(uv);if(id==null)return null;arena.Paused=true;arena.PlayerSelected?.Invoke(id);return id;
        }
        public string FindPlayerAt(Vector2 uv)
        {
            if(arena==null||surface==null||arena.QuietPresentation||arena.RestartCutOpacity>0||float.IsNaN(uv.x)||float.IsNaN(uv.y)||uv.x<0||uv.x>1||uv.y<0||uv.y>1)return null;
            string labelled=names.FindAt(new Vector2(uv.x*surface.contentRect.width,(1-uv.y)*surface.contentRect.height));if(labelled!=null)return labelled;
            var ray=arena.MatchCamera.ViewportPointToRay(new Vector3(uv.x,uv.y,0));
            if(!arena.GoalReplayActive&&Physics.Raycast(ray,out var hit,270)){var player=hit.collider.GetComponentInParent<PlayerView>();if(player!=null)return player.PlayerId;}
            string nearest=null;float best=18*18;var size=surface.contentRect.size;
            for(int i=0;i<arena.Simulation.State.actors.Length;i++){var actor=arena.Simulation.State.actors[i];var view=arena.PlayerVisual(i);if(view==null||!view.gameObject.activeInHierarchy||!arena.GoalReplayActive&&actor.sentOff)continue;var point=arena.GoalReplayActive?view.transform.position+Vector3.up*.9f:new Vector3(actor.position.x,.9f,actor.position.z);var projected=arena.MatchCamera.WorldToViewportPoint(point);if(projected.z<=0||projected.x<0||projected.x>1||projected.y<0||projected.y>1)continue;float dx=(projected.x-uv.x)*size.x,dy=(projected.y-uv.y)*size.y;float distance=dx*dx+dy*dy;if(distance<best){nearest=actor.id;best=distance;}}
            return nearest;
        }
        void LateUpdate()
        {
            if(!Bound||arena==null||arena.QuietPresentation){names?.Hide();locator?.Hide();restartCut?.Hide();return;}var size=surface.contentRect.size;
            if(size.x<8||size.y<8||float.IsNaN(size.x)||float.IsNaN(size.y)){names.Hide();locator?.Hide();restartCut?.Hide();return;}
            float density=TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.scale;
            int quality=PlayerPrefs.GetInt("render-quality",1),samples=RenderBudget.MsaaSamples(quality);
            var resolution=RenderBudget.ViewportSize(quality,size.x,size.y,density);int width=resolution.x,height=resolution.y;
            if(texture==null||texture.width!=width||texture.height!=height||requestedMsaa!=samples){Release();requestedMsaa=samples;texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="Touchline match viewport",filterMode=FilterMode.Bilinear};var descriptor=texture.descriptor;descriptor.msaaSamples=samples;texture.antiAliasing=Mathf.Max(1,SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));texture.Create();surface.image=texture;}
            else if(!texture.IsCreated()){texture.Create();surface.MarkDirtyRepaint();}
            arena.MatchCamera.targetTexture=texture;
            arena.MatchCamera.aspect=width/(float)height;arena.ReframeCamera();
            names.Refresh();
            locator.Refresh();
            restartCut?.Refresh();
            if(arena.RestartCutOpacity>0){names.Hide();locator.Hide();}else if(arena.ExitBallIsOutgoing)locator.Hide();
        }
        void Release(){if(texture==null)return;if(arena!=null&&arena.MatchCamera!=null&&arena.MatchCamera.targetTexture==texture)arena.MatchCamera.targetTexture=null;if(surface!=null)surface.image=null;texture.Release();Destroy(texture);texture=null;}
        void OnDestroy(){names?.Dispose();locator?.Dispose();restartCut?.Dispose();Release();}
    }
}
