using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    // The same gesture state handles touch and mouse. A pinch or a drag cannot
    // turn into an accidental player selection when the final finger lifts.
    public sealed class MatchTouchGesture
    {
        readonly Dictionary<int,Vector2> pointers=new Dictionary<int,Vector2>();
        Vector2 origin;int primary;float spacing;bool suppressed;
        public void Reset(){pointers.Clear();spacing=0;suppressed=false;}
        float Distance(){var e=pointers.Values.GetEnumerator();e.MoveNext();var a=e.Current;e.MoveNext();return Vector2.Distance(a,e.Current);}
        public void Down(int id,Vector2 point){if(pointers.Count==0){origin=point;primary=id;suppressed=false;}pointers[id]=point;if(pointers.Count>=2){suppressed=true;spacing=Distance();}}
        public float Move(int id,Vector2 point){if(!pointers.ContainsKey(id))return 1;pointers[id]=point;if(id==primary&&Vector2.Distance(origin,point)>12)suppressed=true;if(pointers.Count<2)return 1;float distance=Distance();float scale=distance>=8&&spacing>=8?spacing/distance:1;spacing=distance;return scale;}
        public bool Up(int id,Vector2 point){if(!pointers.ContainsKey(id))return false;bool tap=pointers.Count==1&&id==primary&&!suppressed&&Vector2.Distance(origin,point)<=12;pointers.Remove(id);spacing=pointers.Count>=2?Distance():0;return tap;}
        public void Cancel(int id){pointers.Remove(id);suppressed=true;spacing=0;}
    }
}
