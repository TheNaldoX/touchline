// Offline replay adapter only. Future recorded samples are available to this benchmark;
// this is not an online prediction algorithm and never writes into Touchline Core.
using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MotionMatching
{
    public sealed class RecordedMatchController : MotionMatchingCharacterController
    {
        public string TraceFile;
        public int TrackIndex;
        public float Elapsed { get; private set; }
        public float MaxRootCorrection,MaxYawCorrection,MaxRecordedError;
        public double TotalRootCorrection;
        public int RootSamples;
        static string cachedPath;
        static Recording cached;
        Recording Data {get{
            if(cached==null||cachedPath!=TraceFile){cached=JsonUtility.FromJson<Recording>(File.ReadAllText(TraceFile));cachedPath=TraceFile;Validate(cached);}
            return cached;
        }}
        Sample[] Samples=>Data.tracks[TrackIndex].samples;
        public static void Validate(Recording data){
            if(data==null||data.hz<=0||data.tracks==null||data.tracks.Length!=22)throw new Exception("Invalid match trace");
            int count=data.tracks[0].samples.Length;
            if(count<data.hz*24+1)throw new Exception("Trace must cover benchmark and future horizon");
            foreach(var t in data.tracks){
                if(t.samples==null||t.samples.Length!=count)throw new Exception("Unequal tracks");
                foreach(var s in t.samples)if(float.IsNaN(s.x)||float.IsInfinity(s.x)||float.IsNaN(s.z)||float.IsInfinity(s.z)||float.IsNaN(s.angle)||float.IsInfinity(s.angle))throw new Exception("Invalid coordinate");
            }
        }
        public static Sample Interpolate(Sample[] samples,float frame){
            float bounded=Mathf.Clamp(frame,0,samples.Length-1);int i=(int)bounded;
            var a=samples[i];var b=samples[Math.Min(i+1,samples.Length-1)];float t=bounded-i;
            return new Sample{x=Mathf.Lerp(a.x,b.x,t),z=Mathf.Lerp(a.z,b.z,t),
                angle=Mathf.LerpAngle(a.angle*Mathf.Rad2Deg,b.angle*Mathf.Rad2Deg,t)*Mathf.Deg2Rad};
        }
        Sample At(float time)=>Interpolate(Samples,time*Data.hz);
        static float3 Position(Sample s)=>new float3(s.x,0,s.z);
        static float3 Direction(Sample s)=>new float3(math.sin(s.angle),0,math.cos(s.angle));
        protected override void OnUpdate(){Elapsed=Mathf.Min(Elapsed+Time.deltaTime,(Samples.Length-1f)/Data.hz);}
        public override float3 GetWorldInitPosition()=>Position(At(0));
        public override float3 GetWorldInitDirection()=>Direction(At(0));
        public override float3 GetPosition()=>Position(At(Elapsed));
        public override float GetTargetSpeed()=>math.distance(Position(At(Elapsed)),Position(At(Elapsed+1f/Data.hz)))*Data.hz;
        public override void GetTrajectoryFeature(MotionMatchingData.TrajectoryFeature feature,int index,Transform character,NativeArray<float> output){
            var sample=At(Elapsed+feature.FramesPrediction[index]*DatabaseDeltaTime);
            Vector3 local;
            if(feature.FeatureType==MotionMatchingData.TrajectoryFeature.Type.Position)local=character.InverseTransformPoint(Position(sample));
            else if(feature.FeatureType==MotionMatchingData.TrajectoryFeature.Type.Direction)local=character.InverseTransformDirection(Direction(sample));
            else throw new NotSupportedException("Trace adapter only supports planar position/direction");
            output[0]=local.x;output[1]=local.z;
        }
        public void ApplyRecordedRoot(MotionMatchingController mm,Transform root){
            var sample=At(Elapsed);Vector3 target=Position(sample);
            Quaternion rotation=Quaternion.LookRotation(Direction(sample),Vector3.up);
            Vector3 correction=target-root.position;
            MaxRootCorrection=Mathf.Max(MaxRootCorrection,correction.magnitude);
            TotalRootCorrection+=correction.magnitude;RootSamples++;
            MaxYawCorrection=Mathf.Max(MaxYawCorrection,Quaternion.Angle(root.rotation,rotation));
            mm.SetPosAdjustment(correction);mm.SetRotAdjustment(rotation*Quaternion.Inverse(root.rotation));
            // Position and facing are interpolated from immutable Core samples, not from root motion.
            root.SetPositionAndRotation(target,rotation);
            MaxRecordedError=Mathf.Max(MaxRecordedError,Vector3.Distance(root.position,target));
        }
        public static void CheckInterpolation(){
            var samples=new[]{new Sample{x=0,z=0,angle=179*Mathf.Deg2Rad},new Sample{x=2,z=4,angle=-179*Mathf.Deg2Rad}};
            var mid=Interpolate(samples,.5f);
            if(Mathf.Abs(mid.x-1)>.0001f||Mathf.Abs(mid.z-2)>.0001f||Mathf.Abs(Mathf.DeltaAngle(mid.angle*Mathf.Rad2Deg,180))>.001f)throw new Exception("Trace midpoint/wrap failed");
            if(Interpolate(samples,-1).x!=0||Interpolate(samples,5).x!=2||samples[0].angle!=179*Mathf.Deg2Rad)throw new Exception("Trace bounds/immutability failed");
        }
        [Serializable] public class Recording{public int hz;public Track[] tracks;}
        [Serializable] public class Track{public string id;public int side,slot;public Sample[] samples;}
        [Serializable] public struct Sample{public float x,z,angle;}
    }
}
