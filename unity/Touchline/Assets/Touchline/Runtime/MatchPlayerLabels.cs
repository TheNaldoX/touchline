using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    public enum MatchNameDisplay { Off,NearBall,All }
    // One retained UI pool; projected after the final viewport camera framing.
    // Picking stays on the Image so tapping a name uses the same drag/pinch guard.
    public sealed class MatchPlayerLabels
    {
        public const int Capacity=22;
        readonly Label[] labels=new Label[Capacity];
        readonly string[] identities=new string[Capacity];
        readonly float[] textWidths=new float[Capacity],measuredFontSizes=new float[Capacity];
        readonly Rect[] bounds=new Rect[Capacity];
        readonly Vector3[] projected=new Vector3[Capacity];
        readonly bool[] visible=new bool[Capacity];
        readonly bool[] desired=new bool[Capacity];
        readonly bool[] carriers=new bool[Capacity];
        readonly int[] order=new int[Capacity];
        readonly VisualElement layer=new VisualElement{name="match-name-layer",pickingMode=PickingMode.Ignore};
        MatchArena arena;Image surface;bool shown;Vector2 lastSize;
        public int IdentityUpdates {get;private set;}
        public int VisibleCount {get;private set;}
        public VisualElement Layer=>layer;
        public Label LabelAt(int index)=>labels[index];
        public Rect BoundsAt(int index)=>bounds[index];
        public string IdentityAt(int index)=>identities[index];
        public bool VisibleAt(int index)=>visible[index]&&shown;
        // Fallback for older profiles without a separately documented surname.
        // Retain common family-name particles and single-name football identities.
        public static string Surname(string fullName,string nationality=null)
        {
            if(string.IsNullOrWhiteSpace(fullName))return "Joueur";
            var words=fullName.Trim().Split(new[]{' '},System.StringSplitOptions.RemoveEmptyEntries);
            // Imported Korean international names use family-first ordering.
            if(words.Length>1&&nationality=="South Korea")return words[0];
            int last=words.Length-1;
            if(last>0&&(words[last].Equals("Jr.",System.StringComparison.OrdinalIgnoreCase)||words[last].Equals("Sr.",System.StringComparison.OrdinalIgnoreCase)))last--;
            int first=last;
            while(first>0&&FamilyParticle(words[first-1]))first--;
            return string.Join(" ",words,first,last-first+1);
        }
        static bool FamilyParticle(string word)=>word.ToLowerInvariant() is "de" or "del" or "della" or "di" or "da" or "dos" or "das" or "do" or "du" or "des" or "van" or "von" or "der" or "den" or "ter" or "ten" or "la" or "le" or "el" or "al" or "bin" or "ben" or "mac";
        public MatchPlayerLabels()
        {
            layer.AddToClassList("match-name-layer");layer.style.position=Position.Absolute;layer.style.overflow=Overflow.Hidden;
            for(int i=0;i<Capacity;i++){
                var label=new Label{name="match-name-"+i,pickingMode=PickingMode.Ignore};label.AddToClassList("match-name");
                label.AddToClassList(i<11?"match-name-home":"match-name-away");
                label.style.position=Position.Absolute;label.style.display=DisplayStyle.None;
                labels[i]=label;layer.Add(label);
            }
            layer.style.display=DisplayStyle.None;
        }
        public void Bind(MatchArena match,Image image)
        {
            layer.RemoveFromHierarchy();arena=match;surface=image;lastSize=Vector2.zero;surface?.Add(layer);Hide();
        }
        public void Dispose(){Hide();layer.RemoveFromHierarchy();arena=null;surface=null;}
        public void Hide(){if(shown)layer.style.display=DisplayStyle.None;shown=false;VisibleCount=0;}
        public string FindAt(Vector2 local)
        {
            if(!shown)return null;
            // Reverse paint order agrees with the actual label visible at the touch point.
            for(int i=Capacity-1;i>=0;i--)if(visible[i]&&bounds[i].Contains(local))return identities[i];
            return null;
        }
        public static bool Project(Vector3 viewport,Vector2 size,out Vector2 anchor)
        {
            anchor=Vector2.zero;if(!Finite(viewport.x)||!Finite(viewport.y)||!Finite(viewport.z)||!Finite(size.x)||!Finite(size.y)||viewport.z<=0||viewport.x<0||viewport.x>1||viewport.y<0||viewport.y>1||size.x<40||size.y<40)return false;
            anchor=new Vector2(viewport.x*size.x,(1-viewport.y)*size.y);return true;
        }
        public static Rect Place(Vector2 anchor,Vector2 size,float width,Rect[] occupied,int count)
        {
            const float height=19;var original=new Rect(Mathf.Clamp(anchor.x-width*.5f,2,Mathf.Max(2,size.x-width-2)),Mathf.Clamp(anchor.y-height-5,2,Mathf.Max(2,size.y-height-2)),width,height);
            // At most two short upward steps: names remain near their own player.
            var best=original;int bestConflicts=int.MaxValue;
            for(int attempt=0;attempt<3;attempt++){
                var candidate=original;candidate.y=Mathf.Max(2,original.y-attempt*20);int conflicts=0;
                for(int i=0;i<count;i++)if(candidate.Overlaps(occupied[i]))conflicts++;
                if(conflicts<bestConflicts){best=candidate;bestConflicts=conflicts;}if(conflicts==0)break;
            }
            return best;
        }
        public static bool TryPlace(Vector2 anchor,Vector2 size,float width,Rect[] occupied,int count,out Rect rectangle)
        {
            rectangle=Place(anchor,size,width,occupied,count);
            for(int i=0;i<count;i++)if(rectangle.Overlaps(occupied[i]))return false;
            return true;
        }
        readonly Rect[] occupied=new Rect[Capacity];
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public void Refresh()
        {
            if(arena==null||arena.NameDisplay==MatchNameDisplay.Off||surface==null||surface.panel==null||surface.resolvedStyle.display==DisplayStyle.None||arena.QuietPresentation||arena.MatchCamera==null||!arena.MatchCamera.enabled||arena.Viewport?.Texture==null||!arena.Viewport.Texture.IsCreated()){Hide();return;}
            var size=surface.contentRect.size;if(!Finite(size.x)||!Finite(size.y)||size.x<40||size.y<40){Hide();return;}
            if(lastSize!=size){lastSize=size;System.Array.Clear(textWidths,0,Capacity);layer.style.left=surface.contentRect.x;layer.style.top=surface.contentRect.y;layer.style.width=size.x;layer.style.height=size.y;}
            if(!shown){shown=true;layer.style.display=DisplayStyle.Flex;}
            var state=arena.Simulation.State;int count=0;VisibleCount=0;
            System.Array.Clear(desired,0,Capacity);
            for(int i=0;i<Capacity;i++){
                var actor=state.actors[i];var view=arena.PlayerVisual(i);
                if(identities[i]!=actor.id){identities[i]=actor.id;labels[i].text=arena.PlayerSurname(actor.id);labels[i].tooltip=arena.PlayerDisplayName(actor.id);textWidths[i]=0;IdentityUpdates++;}
                projected[i]=view==null?new Vector3(0,0,-1):arena.MatchCamera.WorldToViewportPoint(view.LabelHeadPosition+Vector3.up*.12f);
                bool replay=arena.GoalReplayActive;
                bool carrier=!replay&&actor.id==state.ball.owner;
                bool involved=replay?view!=null&&Vector3.ProjectOnPlane(view.transform.position-arena.BallDisplayPosition,Vector3.up).sqrMagnitude<196:carrier||(actor.position-state.ball.position).Length<14||(state.ball.elapsed>=0&&actor.id==state.ball.to);
                bool valid=(replay||!actor.sentOff)&&view!=null&&view.gameObject.activeInHierarchy&&(arena.NameDisplay==MatchNameDisplay.All||involved)&&Project(projected[i],size,out _);
                if(valid)order[count++]=i;
                if(carriers[i]!=carrier){carriers[i]=carrier;labels[i].EnableInClassList("match-name-carrier",carrier);}
            }
            // Near players have priority when two names share a screen region.
            for(int i=1;i<count;i++){int item=order[i],j=i-1;while(j>=0&&Priority(order[j])>Priority(item)){order[j+1]=order[j];j--;}order[j+1]=item;}
            int placed=0;
            for(int p=0;p<count;p++){
                if(arena.NameDisplay==MatchNameDisplay.NearBall&&placed>=6)break;
                int i=order[p];Project(projected[i],size,out var anchor);
                // Cache real glyph widths. Character counts underestimate bold
                // surnames when responsive styles change the font size.
                var label=labels[i];float fontSize=label.resolvedStyle.fontSize;
                if(textWidths[i]<=0||measuredFontSizes[i]!=fontSize){
                    float measured=label.MeasureTextSize(label.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x;
                    if(measured>0&&!float.IsNaN(measured)){textWidths[i]=measured+12;measuredFontSizes[i]=fontSize;}
                }
                float width=Mathf.Clamp(textWidths[i]>0?textWidths[i]:label.text.Length*6.7f+12,44,Mathf.Min(180,size.x-4));
                if(!TryPlace(anchor,size,width,occupied,placed,out var rect))continue;
                occupied[placed++]=rect;desired[i]=true;
                if(bounds[i]!=rect){bounds[i]=rect;labels[i].style.left=rect.x;labels[i].style.top=rect.y;labels[i].style.width=rect.width;labels[i].style.height=rect.height;}
                VisibleCount++;
            }
            for(int i=0;i<Capacity;i++)SetVisible(i,desired[i]);
            float Priority(int index){var actor=state.actors[index];if(arena.GoalReplayActive)return arena.NameDisplay==MatchNameDisplay.NearBall?Vector3.ProjectOnPlane(arena.PlayerVisual(index).transform.position-arena.BallDisplayPosition,Vector3.up).magnitude:projected[index].z;if(carriers[index])return -3;if(actor.id==state.ball.from&&state.ball.elapsed>=0)return -2;if(actor.id==state.ball.to&&state.ball.elapsed>=0)return -1;return arena.NameDisplay==MatchNameDisplay.NearBall?(actor.position-state.ball.position).Length:projected[index].z;}
        }
        void SetVisible(int index,bool value){if(visible[index]==value)return;visible[index]=value;labels[index].style.display=value?DisplayStyle.Flex:DisplayStyle.None;if(value)textWidths[index]=0;}
    }
}
