using UnityEngine;

namespace Touchline
{
    public sealed partial class PlayerView
    {
        bool keeperAppearance;
        SkinnedMeshRenderer keeperGloves;Material glovePalm,gloveBack;
        void SetKeeperAppearance(bool keeper)
        {
            keeperAppearance=keeper;ApplyKit();
            if(keeper&&keeperGloves==null){
                var go=new GameObject("Keeper gloves");go.transform.SetParent(body,false);keeperGloves=go.AddComponent<SkinnedMeshRenderer>();
                keeperGloves.sharedMesh=KeeperGloveMesh.Get(source,geometry.bindposes);keeperGloves.bones=skeleton;keeperGloves.rootBone=body;
                glovePalm=Material(new Color(.90f,.91f,.86f));gloveBack=Material(new Color(.055f,.10f,.13f));glovePalm.SetFloat("_Smoothness",.32f);
                keeperGloves.sharedMaterials=new[]{glovePalm,gloveBack};keeperGloves.localBounds=new Bounds(new Vector3(0,.9f,0),new Vector3(4.6f,3.8f,3.6f));keeperGloves.updateWhenOffscreen=false;
            }
            if(keeperGloves!=null)keeperGloves.enabled=keeper;
        }
    }
}
