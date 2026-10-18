using System;
using MotionMatching;
using UnityEngine;

public static class FootContactChecks
{
    [Serializable] public class Result { public int cases; public float maxBeforeToeErrorMetres,maxAfterToeErrorMetres; }
    public static Result Run()
    {
        var result=new Result();
        foreach(float side in new[]{-1f,1f})foreach(float yaw in new[]{0f,65f}){
            var root=new GameObject("ContactCheck");
            try{
                var hip=root.transform;
                var knee=new GameObject("Knee").transform;knee.SetParent(hip);
                var ankle=new GameObject("Ankle").transform;ankle.SetParent(knee);
                var toe=new GameObject("Toe").transform;toe.SetParent(ankle);
                hip.position=new Vector3(0,1,0);knee.position=new Vector3(0,.55f,.15f);
                ankle.position=new Vector3(0,.1f,0);toe.position=new Vector3(0,.1f,.2f);
                hip.rotation=Quaternion.Euler(0,yaw,0);
                var hipRotation=hip.rotation;var kneeRotation=knee.rotation;var ankleRotation=ankle.rotation;
                var shift=hipRotation*new Vector3(side*.08f,.02f,.03f);
                var ankleTarget=ankle.position+shift;var toeTarget=toe.position+shift;
                var forward=hipRotation*Vector3.forward;
                TwoJointIK.Solve(ankleTarget,hip,knee,ankle,forward);
                float before=Vector3.Distance(toe.position,toeTarget);
                hip.rotation=hipRotation;knee.rotation=kneeRotation;ankle.rotation=ankleRotation;
                FootContactIK.Solve(ankleTarget,hip,knee,ankle,forward);
                float after=Vector3.Distance(toe.position,toeTarget);
                if(before<.002f||after>.002f||Quaternion.Angle(ankle.rotation,ankleRotation)>.01f)
                    throw new Exception($"Toe contact failed: side={side}, yaw={yaw}, before={before}, after={after}");
                result.cases++;result.maxBeforeToeErrorMetres=Mathf.Max(result.maxBeforeToeErrorMetres,before);
                result.maxAfterToeErrorMetres=Mathf.Max(result.maxAfterToeErrorMetres,after);
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return result;
    }
}
