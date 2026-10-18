// Editor diagnostics only: no game logic and no source-pose mutation.
using System;
using System.Collections.Generic;
using System.Reflection;
using MotionMatching;
using UnityEngine;

[Serializable]
public sealed class PoseMetrics
{
    public int jointSamples, stationaryJointSamples, lockedToeSamples;
    public float stationaryJointPercent, maxJointDegreesPerSecond, p95JointDegreesPerSecond;
    public float maxLockedToeMetresPerSecond, p95LockedToeMetresPerSecond;
    readonly List<float> jointSpeeds=new List<float>(), toeSpeeds=new List<float>();
    Quaternion[][] previousRotations;
    Vector3[][] previousToes;
    bool[][] previousLocked;
    int[][] toes;
    readonly FieldInfo[] locks={Field("IsLeftFootContact"),Field("IsRightFootContact")};
    static FieldInfo Field(string name)=>typeof(MotionMatchingController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);

    public void Sample(MotionMatchingController[] actors,int hz,bool measure)
    {
        if(previousRotations==null){
            previousRotations=new Quaternion[actors.Length][];previousToes=new Vector3[actors.Length][];
            previousLocked=new bool[actors.Length][];toes=new int[actors.Length][];
            for(int i=0;i<actors.Length;i++){
                previousRotations[i]=new Quaternion[actors[i].SkeletonTransforms.Length];
                previousToes[i]=new Vector3[2];previousLocked[i]=new bool[2];
                toes[i]=new[]{(int)Field("LeftToesIndex").GetValue(actors[i]),(int)Field("RightToesIndex").GetValue(actors[i])};
            }
            measure=false;
        }
        for(int i=0;i<actors.Length;i++){
            var bones=actors[i].SkeletonTransforms;
            for(int j=1;j<bones.Length;j++){
                var rotation=bones[j].localRotation;
                if(measure){
                    // Quaternion.Angle rounds tiny angles to zero; atan2 preserves small changes.
                    var delta=Quaternion.Inverse(previousRotations[i][j])*rotation;
                    float angle=2f*Mathf.Atan2(new Vector3(delta.x,delta.y,delta.z).magnitude,Mathf.Abs(delta.w))*Mathf.Rad2Deg;
                    jointSpeeds.Add(angle*hz);jointSamples++;
                    if(angle<.0001f)stationaryJointSamples++; // Numerical floor, degrees/frame.
                }
                previousRotations[i][j]=rotation;
            }
            for(int side=0;side<2;side++){
                var position=bones[toes[i][side]].position;
                bool locked=(bool)locks[side].GetValue(actors[i]);
                if(measure&&locked&&previousLocked[i][side]){
                    var delta=position-previousToes[i][side];delta.y=0;
                    toeSpeeds.Add(delta.magnitude*hz);lockedToeSamples++;
                }
                previousToes[i][side]=position;previousLocked[i][side]=locked;
            }
        }
    }
    public void Finish(){
        jointSpeeds.Sort();toeSpeeds.Sort();
        stationaryJointPercent=100f*stationaryJointSamples/Math.Max(1,jointSamples);
        if(jointSpeeds.Count>0){maxJointDegreesPerSecond=jointSpeeds[jointSpeeds.Count-1];p95JointDegreesPerSecond=jointSpeeds[(int)(jointSpeeds.Count*.95)];}
        if(toeSpeeds.Count>0){maxLockedToeMetresPerSecond=toeSpeeds[toeSpeeds.Count-1];p95LockedToeMetresPerSecond=toeSpeeds[(int)(toeSpeeds.Count*.95)];}
    }
}
