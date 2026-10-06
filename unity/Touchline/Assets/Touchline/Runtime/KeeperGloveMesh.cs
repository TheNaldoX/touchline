using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    // An outer surface follows the existing finger weights. Both keepers share
    // this mesh; only the few millimetres of padding are new geometry.
    public static class KeeperGloveMesh
    {
        static Mesh shared;
        public static Mesh Get(HumanSource source,Matrix4x4[] bindPoses)
        {
            if(shared!=null)return shared;
            var heads=new Dictionary<string,Vector3>();foreach(var bone in source.bones)heads[bone.name]=PlayerView.Vector(bone.head);
            var wrists=new[]{heads["wrist.L"],heads["wrist.R"]};
            var axes=new[]{(heads["finger3-1.L"]-wrists[0]).normalized,(heads["finger3-1.R"]-wrists[1]).normalized};
            var palms=new[]{Vector3.Cross(heads["finger5-1.L"]-heads["finger2-1.L"],axes[0]).normalized,-Vector3.Cross(heads["finger5-1.R"]-heads["finger2-1.R"],axes[1]).normalized};
            var positions=new List<Vector3>();var normals=new List<Vector3>();var weights=new List<BoneWeight>();var triangles=new[]{new List<int>(),new List<int>()};
            foreach(var part in source.parts){
                if(part.material!=0)continue;var mapped=new Dictionary<int,int>();
                for(int t=0;t<part.index.Length;t+=3){
                    int a=part.index[t],b=part.index[t+1],c=part.index[t+2];
                    var center=(Read(part.position,a)+Read(part.position,b)+Read(part.position,c))/3;
                    int side=Vector3.SqrMagnitude(center-wrists[0])<Vector3.SqrMagnitude(center-wrists[1])?0:1;
                    var offset=center-wrists[side];float along=Vector3.Dot(offset,axes[side]);
                    if(offset.sqrMagnitude>.255f*.255f||along<-.025f)continue;
                    var normal=(Read(part.normal,a)+Read(part.normal,b)+Read(part.normal,c)).normalized;
                    int material=along<.025f||Vector3.Dot(normal,palms[side])<.1f?1:0;
                    for(int k=0;k<3;k++){
                        int v=part.index[t+k];if(!mapped.TryGetValue(v,out int target)){
                            target=positions.Count;mapped.Add(v,target);var n=Read(part.normal,v);
                            positions.Add(Read(part.position,v)+n*.0035f);normals.Add(n);
                            weights.Add(new BoneWeight{boneIndex0=part.skinIndex[v*4],boneIndex1=part.skinIndex[v*4+1],boneIndex2=part.skinIndex[v*4+2],boneIndex3=part.skinIndex[v*4+3],weight0=part.skinWeight[v*4],weight1=part.skinWeight[v*4+1],weight2=part.skinWeight[v*4+2],weight3=part.skinWeight[v*4+3]});
                        }
                        triangles[material].Add(target);
                    }
                }
            }
            shared=new Mesh{name="Articulated keeper gloves"};shared.SetVertices(positions);shared.SetNormals(normals);shared.boneWeights=weights.ToArray();shared.bindposes=bindPoses;shared.subMeshCount=2;
            shared.SetTriangles(triangles[0],0);shared.SetTriangles(triangles[1],1);shared.RecalculateBounds();return shared;
        }
        static Vector3 Read(float[] values,int vertex)=>new Vector3(values[vertex*3],values[vertex*3+1],values[vertex*3+2]);
    }
}
