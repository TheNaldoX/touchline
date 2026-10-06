using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    public enum MatchBallDisplay { Off,Discreet,Enhanced }

    // Accessibility overlay only. The physical ball, contacts and camera retain
    // their original sizes and trajectories. Its centre is never shifted inward.
    public sealed class MatchBallLocator
    {
        readonly VisualElement layer=new VisualElement{name="match-ball-layer",pickingMode=PickingMode.Ignore};
        readonly VisualElement marker=new VisualElement{name="match-ball-marker",pickingMode=PickingMode.Ignore};
        MatchArena arena;Image surface;MatchBallDisplay styled=(MatchBallDisplay)(-1);
        Vector2 lastSize;Rect lastBounds;bool shown;
        public VisualElement Layer=>layer;
        public VisualElement Marker=>marker;
        public bool Visible=>shown;
        public Rect Bounds=>lastBounds;
        public MatchBallLocator()
        {
            layer.style.position=Position.Absolute;layer.style.overflow=Overflow.Hidden;
            marker.style.position=Position.Absolute;marker.style.borderTopLeftRadius=50;
            marker.style.borderTopRightRadius=50;marker.style.borderBottomLeftRadius=50;marker.style.borderBottomRightRadius=50;
            layer.Add(marker);layer.style.display=DisplayStyle.None;
        }
        public void Bind(MatchArena match,Image image)
        {
            layer.RemoveFromHierarchy();arena=match;surface=image;lastSize=Vector2.zero;
            surface?.Add(layer);Hide();
        }
        public void Hide(){if(shown)layer.style.display=DisplayStyle.None;shown=false;}
        public void Dispose(){Hide();layer.RemoveFromHierarchy();arena=null;surface=null;}
        public static bool Project(Vector3 viewport,Vector2 size,MatchBallDisplay mode,out Rect rectangle)
        {
            rectangle=default;
            if(mode!=MatchBallDisplay.Discreet&&mode!=MatchBallDisplay.Enhanced)return false;
            if(!MatchPlayerLabels.Project(viewport,size,out var centre))return false;
            float diameter=mode==MatchBallDisplay.Discreet?10:18;
            rectangle=new Rect(centre.x-diameter*.5f,centre.y-diameter*.5f,diameter,diameter);
            return true;
        }
        // Projection presentation is separated from scene validity so mirrored
        // cameras and all visibility transitions can be checked deterministically.
        public void ShowProjected(Vector3 viewport,Vector2 size,MatchBallDisplay mode)
        {
            if(!Project(viewport,size,mode,out var rectangle)){Hide();return;}
            if(lastSize!=size){lastSize=size;layer.style.width=size.x;layer.style.height=size.y;}
            if(styled!=mode){
                styled=mode;float width=mode==MatchBallDisplay.Discreet?1.25f:2;
                var colour=new Color(1,1,1,mode==MatchBallDisplay.Discreet?.78f:1);
                marker.style.borderTopWidth=marker.style.borderBottomWidth=marker.style.borderLeftWidth=marker.style.borderRightWidth=width;
                marker.style.borderTopColor=marker.style.borderBottomColor=marker.style.borderLeftColor=marker.style.borderRightColor=colour;
                marker.style.backgroundColor=new Color(.015f,.02f,.025f,mode==MatchBallDisplay.Discreet?.12f:.30f);
            }
            if(lastBounds!=rectangle){lastBounds=rectangle;marker.style.left=rectangle.x;marker.style.top=rectangle.y;marker.style.width=rectangle.width;marker.style.height=rectangle.height;}
            if(!shown){shown=true;layer.style.display=DisplayStyle.Flex;}
        }
        public void Refresh()
        {
            if(arena==null||arena.BallDisplay==MatchBallDisplay.Off||surface==null||surface.panel==null||surface.resolvedStyle.display==DisplayStyle.None||arena.QuietPresentation||arena.MatchCamera==null||!arena.MatchCamera.enabled||arena.Viewport?.Texture==null||!arena.Viewport.Texture.IsCreated()){Hide();return;}
            layer.style.left=surface.contentRect.x;layer.style.top=surface.contentRect.y;
            ShowProjected(arena.MatchCamera.WorldToViewportPoint(arena.BallDisplayPosition),surface.contentRect.size,arena.BallDisplay);
        }
    }
}
