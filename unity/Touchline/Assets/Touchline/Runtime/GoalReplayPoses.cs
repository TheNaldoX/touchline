using UnityEngine;

namespace Touchline
{
    // Bounded visual history. No MatchState, events or simulation references.
    public sealed class GoalReplayPoses
    {
        sealed class Frame
        {
            public float time,cameraAspect,cameraFarClip;public readonly Vector3[] positions;public readonly Quaternion[] rotations;public readonly bool[] active;
            public Frame(int count){positions=new Vector3[count];rotations=new Quaternion[count];active=new bool[count];}
        }
        readonly Camera camera;
        readonly Transform[] targets;readonly Frame[] frames=new Frame[241];
        int next,count,cursor;float elapsed,start,end;
        public bool Active{get;private set;}
        public float Progress=>Active&&end>start?Mathf.Clamp01(elapsed/(end-start)):0;
        public int Count=>count;
        public float RecordedCameraAspect{get;private set;}
        public float RecordedCameraFarClip{get;private set;}
        public long EstimatedPayloadBytes=>(long)frames.Length*(targets.Length*29+(camera!=null?8:0));
        public GoalReplayPoses(Transform[] targets,Camera camera=null){this.camera=camera;this.targets=targets;for(int i=0;i<frames.Length;i++)frames[i]=new Frame(targets.Length);}
        Frame At(int index)=>frames[(next-count+frames.Length+index)%frames.Length];
        public void Reset(){Active=false;next=count=cursor=0;elapsed=0;}
        public void Capture(float time,bool force=false)
        {
            if(Active||float.IsNaN(time)||float.IsInfinity(time))return;
            if(count>0){float delta=time-At(count-1).time;if(delta<0){Reset();}else if(!force&&delta<1f/30-.0001f)return;}
            var frame=frames[next];frame.time=time;
            if(camera!=null){frame.cameraAspect=camera.aspect;frame.cameraFarClip=camera.farClipPlane;}
            for(int i=0;i<targets.Length;i++){var t=targets[i];frame.positions[i]=t.localPosition;frame.rotations[i]=t.localRotation;frame.active[i]=t.gameObject.activeSelf;}
            next=(next+1)%frames.Length;count=Mathf.Min(count+1,frames.Length);
            while(count>1&&time-At(0).time>8)count--;
        }
        public bool Begin()
        {
            if(Active||count<3||At(count-1).time-At(0).time<.5f)return false;
            start=At(0).time;end=At(count-1).time;elapsed=0;cursor=0;Active=true;RecordedCameraAspect=At(count-1).cameraAspect;RecordedCameraFarClip=At(count-1).cameraFarClip;return true;
        }
        void Apply(Frame a,Frame b,float alpha)
        {
            // A blend across a recorded viewport change is not a valid camera
            // composition for either aspect, so ask presentation to fit it.
            RecordedCameraAspect=alpha>0&&alpha<1&&Mathf.Abs(a.cameraAspect-b.cameraAspect)>=.002f?0:alpha<1?a.cameraAspect:b.cameraAspect;
            RecordedCameraFarClip=Mathf.Max(a.cameraFarClip,b.cameraFarClip);
            for(int i=0;i<targets.Length;i++){var t=targets[i];t.localPosition=Vector3.Lerp(a.positions[i],b.positions[i],alpha);t.localRotation=Quaternion.Slerp(a.rotations[i],b.rotations[i],alpha);bool visible=alpha<1?a.active[i]:b.active[i];if(t.gameObject.activeSelf!=visible)t.gameObject.SetActive(visible);}
        }
        public void Advance(float seconds)
        {
            if(!Active)return;elapsed+=Mathf.Max(0,seconds);
            if(start+elapsed>=end){Skip();return;}
            while(cursor+1<count-1&&At(cursor+1).time<=start+elapsed)cursor++;
            var a=At(cursor);var b=At(cursor+1);Apply(a,b,Mathf.InverseLerp(a.time,b.time,start+elapsed));
        }
        public void Skip(){if(!Active)return;var live=At(count-1);Apply(live,live,1);Active=false;}
    }
}
