using System.Collections.Generic;
using UnityEngine;

namespace Touchline
{
    // Squelette MakeHuman du joueur décrit comme un « Humanoid » Mecanim : les
    // animations Mixamo (ou toute capture Humanoid) s'appliquent au modèle de
    // Touchline sans changer le maillage, les maillots ni les morphologies.
    public sealed partial class PlayerView
    {
        // Os MakeHuman (game_engine) → os Humanoid Unity.
        static readonly (string bone,string human)[] HumanMap={
            ("root","Hips"),("spine05","Spine"),("spine03","Chest"),("spine01","UpperChest"),("neck01","Neck"),("head","Head"),
            ("clavicle.L","LeftShoulder"),("upperarm01.L","LeftUpperArm"),("lowerarm01.L","LeftLowerArm"),("wrist.L","LeftHand"),
            ("clavicle.R","RightShoulder"),("upperarm01.R","RightUpperArm"),("lowerarm01.R","RightLowerArm"),("wrist.R","RightHand"),
            ("upperleg01.L","LeftUpperLeg"),("lowerleg01.L","LeftLowerLeg"),("foot.L","LeftFoot"),("toe1-1.L","LeftToes"),
            ("upperleg01.R","RightUpperLeg"),("lowerleg01.R","RightLowerLeg"),("foot.R","RightFoot"),("toe1-1.R","RightToes"),
            ("finger1-1.L","Left Thumb Proximal"),("finger1-2.L","Left Thumb Intermediate"),("finger1-3.L","Left Thumb Distal"),
            ("finger2-1.L","Left Index Proximal"),("finger2-2.L","Left Index Intermediate"),("finger2-3.L","Left Index Distal"),
            ("finger3-1.L","Left Middle Proximal"),("finger3-2.L","Left Middle Intermediate"),("finger3-3.L","Left Middle Distal"),
            ("finger4-1.L","Left Ring Proximal"),("finger4-2.L","Left Ring Intermediate"),("finger4-3.L","Left Ring Distal"),
            ("finger5-1.L","Left Little Proximal"),("finger5-2.L","Left Little Intermediate"),("finger5-3.L","Left Little Distal"),
            ("finger1-1.R","Right Thumb Proximal"),("finger1-2.R","Right Thumb Intermediate"),("finger1-3.R","Right Thumb Distal"),
            ("finger2-1.R","Right Index Proximal"),("finger2-2.R","Right Index Intermediate"),("finger2-3.R","Right Index Distal"),
            ("finger3-1.R","Right Middle Proximal"),("finger3-2.R","Right Middle Intermediate"),("finger3-3.R","Right Middle Distal"),
            ("finger4-1.R","Right Ring Proximal"),("finger4-2.R","Right Ring Intermediate"),("finger4-3.R","Right Ring Distal"),
            ("finger5-1.R","Right Little Proximal"),("finger5-2.R","Right Little Intermediate"),("finger5-3.R","Right Little Distal"),
        };

        static Avatar sharedAvatar;
        public Transform RigRoot=>body;

        // Construit (une fois, partagé) l'avatar Humanoid à partir de la pose de
        // repos du squelette. MakeHuman est en pose « A » : les bras sont ramenés à
        // l'horizontale dans la description pour fournir la pose « T » attendue.
        public Avatar HumanoidAvatar()
        {
            if(sharedAvatar!=null)return sharedAvatar;
            var restRotations=new Dictionary<Transform,Quaternion>();
            foreach(var t in body.GetComponentsInChildren<Transform>(true))restRotations[t]=t.localRotation;
            try{
                foreach(var t in body.GetComponentsInChildren<Transform>(true))if(t==body||bones.ContainsKey(t.name))t.localRotation=Quaternion.identity;
                body.localPosition=Vector3.zero; // recalculé par Render à chaque image
                TPoseArm("L");TPoseArm("R");
                var skeleton=new List<SkeletonBone>();
                foreach(var t in body.GetComponentsInChildren<Transform>(true))skeleton.Add(new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale});
                // Côté anatomique déduit de la géométrie (le joueur regarde +Z, sa droite
                // est +X) : si les os « .L » sont à +X, les étiquettes sont inversées.
                bool swap=bones.TryGetValue("upperleg01.L",out var leftLeg)&&body.InverseTransformPoint(leftLeg.position).x>0;
                var human=new List<UnityEngine.HumanBone>();
                foreach(var (bone,humanName) in HumanMap){
                    if(!bones.ContainsKey(bone))continue;
                    string name=humanName;if(swap)name=name.StartsWith("Left")?"Right"+name.Substring(4):name.StartsWith("Right")?"Left"+name.Substring(5):name;
                    human.Add(new UnityEngine.HumanBone{boneName=bone,humanName=name,limit=new HumanLimit{useDefaultValues=true}});
                }
                HumanSidesSwapped=swap;
                var description=new HumanDescription{human=human.ToArray(),skeleton=skeleton.ToArray(),upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,feetSpacing=0,hasTranslationDoF=false};
                sharedAvatar=AvatarBuilder.BuildHumanAvatar(body.gameObject,description);sharedAvatar.name="Touchline footballer";
                return sharedAvatar;
            }finally{foreach(var pair in restRotations)pair.Key.localRotation=pair.Value;}

        }

        // Ramène le bras à l'horizontale, vers l'extérieur du corps (du côté où se
        // trouve l'épaule), avant-bras dans le prolongement.
        void TPoseArm(string side)
        {
            if(!bones.TryGetValue("upperarm01."+side,out var upper)||!bones.TryGetValue("lowerarm01."+side,out var lower))return;
            var outward=body.TransformDirection(new Vector3(Mathf.Sign(body.InverseTransformPoint(upper.position).x),0,0));
            upper.rotation=Quaternion.FromToRotation((lower.position-upper.position).normalized,outward)*upper.rotation;
            if(bones.TryGetValue("wrist."+side,out var wrist))lower.rotation=Quaternion.FromToRotation((wrist.position-lower.position).normalized,outward)*lower.rotation;
        }
        // Vrai si les os « .L » du modèle sont du côté droit du joueur (+X).
        public static bool HumanSidesSwapped {get;private set;}
    }
}
