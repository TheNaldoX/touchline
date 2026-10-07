using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Touchline.Core;

namespace Touchline
{
    // Animation par mouvements capturés (Mixamo, Humanoid) jouée sur le squelette
    // du joueur Touchline via un graphe Playables évalué à chaque image :
    // déplacements (arrêt, course, conduite de balle) mélangés selon la vitesse,
    // gestes (frappe, passe, tête, tacle, gardien…) calés pour que l'instant de
    // contact du clip tombe sur l'instant de contact décidé par le moteur.
    public sealed partial class PlayerView
    {
        // Réglage : animations capturées (true) ou animation procédurale historique.
        public static bool UseMecanim=true;

        static Dictionary<string,AnimationClip> mecanimClips;
        static Dictionary<string,AnimationClip> MecanimClips{get{
            if(mecanimClips==null){mecanimClips=new Dictionary<string,AnimationClip>();foreach(var clip in Resources.LoadAll<AnimationClip>("Animations/Mixamo"))if(clip!=null&&!clip.name.StartsWith("__preview__"))mecanimClips[clip.name]=clip;}
            return mecanimClips;}}
        const string IdleClip="Soccer Idle",JogClip="Jog Forward",DribbleClip="Dribble",KeeperIdleClip="Goalkeeper Idle";
        public static bool MecanimReady=>MecanimClips.ContainsKey(IdleClip)&&MecanimClips.ContainsKey(JogClip);

        // Vitesse (m/s) à laquelle le clip de course/conduite est joué à vitesse 1.
        const float JogNaturalSpeed=3.4f,DribbleNaturalSpeed=3.0f;
        const float MoveBlendFrom=.25f,MoveBlendTo=1.3f;     // m/s : de l'arrêt à la course
        const float ActionFadeIn=.10f,ActionFadeOut=.22f;     // s
        const float LocomotionBlendRate=8f;                  // 1/s : lissage arrêt/course/conduite

        // Instant (s dans le clip) où le pied, la tête ou les mains touchent le ballon.
        // Mesuré sur les clips (vitesse maximale du membre, voir MecanimPrototypeFilm).
        static readonly Dictionary<string,float> ClipContact=new Dictionary<string,float>{
            // Pic de vitesse du pied qui frappe (analyse 60 Hz des clips Mixamo).
            {"Kick Soccerball",.517f},{"Kick Soccerball (1)",.417f},{"Soccer Pass",.433f},{"Chip",.483f},{"Soccer Penalty Kick",.733f},{"Goalkeeper Drop Kick",2.083f},
            // Pic de vitesse des mains.
            {"Throw In",1.60f},{"Goalkeeper Pass",1.133f},{"Goalkeeper Diving Save",1.25f},{"Goalkeeper Catch",.25f},
            // Estimés (pas de membre dominant mesuré) : tête, tacle glissé, chute.
            {"Header",.55f},{"Soccer Header",.95f},{"Soccer Tackle",.45f},{"Soccer Trip",.70f},
        };

        Animator mecanimAnimator;PlayableGraph mecanimGraph;AnimationMixerPlayable mecanimMixer;
        AnimationClipPlayable idlePlayable,jogPlayable,dribblePlayable;
        readonly AnimationClipPlayable[] actionPlayables=new AnimationClipPlayable[2];
        readonly float[] actionWeights=new float[2];readonly bool[] actionLive=new bool[2];
        int activeAction=-1;string mecanimActionKey;int mecanimActionSequence=-1;float mecanimActionElapsed,mecanimActionContact;
        float moveBlend,dribbleBlend;bool mecanimKeeper;

        bool EnsureMecanimGraph(bool keeper)
        {
            if(mecanimGraph.IsValid()&&mecanimKeeper==keeper)return true;
            if(!MecanimReady)return false;
            DisposeMecanim();mecanimKeeper=keeper;
            if(mecanimAnimator==null){mecanimAnimator=body.gameObject.GetComponent<Animator>();if(mecanimAnimator==null)mecanimAnimator=body.gameObject.AddComponent<Animator>();}
            mecanimAnimator.avatar=HumanoidAvatar();mecanimAnimator.applyRootMotion=false;mecanimAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            mecanimGraph=PlayableGraph.Create("Touchline player "+PlayerId);mecanimGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output=AnimationPlayableOutput.Create(mecanimGraph,"Body",mecanimAnimator);
            mecanimMixer=AnimationMixerPlayable.Create(mecanimGraph,5);output.SetSourcePlayable(mecanimMixer);
            AnimationClipPlayable Loop(string name){var clip=MecanimClips.TryGetValue(name,out var c)?c:MecanimClips[IdleClip];var p=AnimationClipPlayable.Create(mecanimGraph,clip);p.SetApplyFootIK(true);
                // Phase de départ propre à chaque joueur : pas de foulées synchronisées.
                p.SetTime(clip.length*((motionIdentity%97)/97f));return p;}
            idlePlayable=Loop(keeper&&MecanimClips.ContainsKey(KeeperIdleClip)?KeeperIdleClip:IdleClip);jogPlayable=Loop(JogClip);dribblePlayable=Loop(MecanimClips.ContainsKey(DribbleClip)?DribbleClip:JogClip);
            mecanimGraph.Connect(idlePlayable,0,mecanimMixer,0);mecanimGraph.Connect(jogPlayable,0,mecanimMixer,1);mecanimGraph.Connect(dribblePlayable,0,mecanimMixer,2);
            for(int i=0;i<2;i++){actionWeights[i]=0;actionLive[i]=false;}activeAction=-1;mecanimActionKey=null;mecanimActionSequence=-1;
            return true;
        }

        void DisposeMecanim(){if(mecanimGraph.IsValid())mecanimGraph.Destroy();}

        // Geste du moteur → (clip, instant de contact côté moteur en s depuis le début du geste).
        bool leftFootedNow;
        bool MecanimAction(Actor actor,out string clip,out float simContact)
        {
            clip=null;simContact=0;
            switch(actor.action){
                case "kick":
                    // Kick Soccerball frappe du pied gauche, Kick Soccerball (1) du droit.
                    clip=actor.actionKind=="shot"?(leftFootedNow?"Kick Soccerball":"Kick Soccerball (1)"):actor.actionKind=="cross"||actor.actionKind=="switch"||actor.actionKind=="clearance"?"Chip":"Soccer Pass";
                    if(actor.slot==0&&MecanimClips.ContainsKey("Goalkeeper Drop Kick")&&actor.actionKind=="clearance")clip="Goalkeeper Drop Kick";
                    simContact=.18f;break;
                case "header":clip="Header";simContact=actor.actionContactTime>0?actor.actionContactTime:.12f;break;
                case "slide":clip="Soccer Tackle";simContact=actor.actionContactTime>0?actor.actionContactTime:.2f;break;
                case "fall":clip="Soccer Trip";simContact=0;break;
                case "throw":clip=actor.slot==0?"Goalkeeper Pass":"Throw In";simContact=actor.actionContactTime>0?actor.actionContactTime:.5f;break;
                case "dive":clip="Goalkeeper Diving Save";simContact=actor.actionContactTime>0?actor.actionContactTime:.2f;break;
                case "claim":clip="Goalkeeper Catch";simContact=.2f;break;
            }
            return clip!=null&&MecanimClips.ContainsKey(clip);
        }

        // Retourne false si l'animation capturée n'est pas disponible (repli procédural).
        bool MecanimRender(Actor actor,float dt,bool reset,float poseSpeed,bool carrying)
        {
            if(!UseMecanim||!EnsureMecanimGraph(actor.slot==0))return false;leftFootedNow=leftFooted;
            if(reset){moveBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(MoveBlendFrom,MoveBlendTo,poseSpeed));dribbleBlend=carrying?1:0;}
            float k=1-Mathf.Exp(-dt*LocomotionBlendRate);
            moveBlend=Mathf.Lerp(moveBlend,Mathf.SmoothStep(0,1,Mathf.InverseLerp(MoveBlendFrom,MoveBlendTo,poseSpeed)),k);
            dribbleBlend=Mathf.Lerp(dribbleBlend,carrying?1:0,k);
            jogPlayable.SetSpeed(Mathf.Clamp(poseSpeed/JogNaturalSpeed,.7f,2.1f));dribblePlayable.SetSpeed(Mathf.Clamp(poseSpeed/DribbleNaturalSpeed,.7f,2.1f));

            // Gestes : nouveau geste → emplacement libre, fondu d'entrée, temps calé sur le contact.
            bool acting=MecanimAction(actor,out var clipName,out var simContact);
            string key=acting?clipName:null;
            if(acting&&(key!=mecanimActionKey||actor.actionSequence!=mecanimActionSequence)){
                int slot=activeAction<0?0:1-activeAction;
                if(actionPlayables[slot].IsValid()){mecanimGraph.Disconnect(mecanimMixer,3+slot);actionPlayables[slot].Destroy();}
                var clip=MecanimClips[clipName];actionPlayables[slot]=AnimationClipPlayable.Create(mecanimGraph,clip);actionPlayables[slot].SetApplyFootIK(true);
                mecanimGraph.Connect(actionPlayables[slot],0,mecanimMixer,3+slot);actionLive[slot]=true;if(activeAction>=0)actionLive[activeAction]=false;
                activeAction=slot;mecanimActionKey=key;mecanimActionSequence=actor.actionSequence;mecanimActionElapsed=0;
                mecanimActionContact=(ClipContact.TryGetValue(clipName,out var c)?c:clip.length*.4f)-simContact;
                if(reset)actionWeights[slot]=1;
            }else if(!acting&&activeAction>=0){actionLive[activeAction]=false;mecanimActionKey=null;}
            if(activeAction>=0&&actionLive[activeAction]){
                mecanimActionElapsed+=dt;var playable=actionPlayables[activeAction];float length=(float)playable.GetAnimationClip().length;
                playable.SetTime(Mathf.Clamp(mecanimActionContact+mecanimActionElapsed,0,length-.001f));playable.SetSpeed(0);
            }
            for(int i=0;i<2;i++){
                if(!actionPlayables[i].IsValid())continue;
                float target=actionLive[i]?1:0;float rate=actionLive[i]?1/ActionFadeIn:1/ActionFadeOut;
                actionWeights[i]=Mathf.MoveTowards(actionWeights[i],target,rate*dt);
                if(!actionLive[i]){actionPlayables[i].SetSpeed(1);} // le geste finit naturellement pendant le fondu de sortie
                if(!actionLive[i]&&actionWeights[i]<=0){mecanimGraph.Disconnect(mecanimMixer,3+i);actionPlayables[i].Destroy();if(activeAction==i)activeAction=-1;}
            }
            float a0=actionPlayables[0].IsValid()?actionWeights[0]:0,a1=actionPlayables[1].IsValid()?actionWeights[1]:0;
            // Deux gestes en fondu enchaîné : leur somme ne dépasse pas 1.
            if(a0+a1>1){float scale=1/(a0+a1);a0*=scale;a1*=scale;}
            float locomotion=1-(a0+a1);
            mecanimMixer.SetInputWeight(0,locomotion*(1-moveBlend));
            mecanimMixer.SetInputWeight(1,locomotion*moveBlend*(1-dribbleBlend));
            mecanimMixer.SetInputWeight(2,locomotion*moveBlend*dribbleBlend);
            mecanimMixer.SetInputWeight(3,a0);mecanimMixer.SetInputWeight(4,a1);
            body.localPosition=Vector3.zero;body.localRotation=Quaternion.identity;
            mecanimGraph.Evaluate(reset?0:Mathf.Max(0,dt));
            MecanimFootLock(dt,reset);
            return true;
        }

        // Verrouillage des pieds : un pied qui touche le sol reste à sa place sur la
        // pelouse pendant l'appui (la jambe s'ajuste par IK) au lieu de glisser
        // quand la foulée du clip ne correspond pas exactement à la vitesse réelle.
        const float FootContactHeight=.12f; // m (cheville), joueur de 1,82 m
        const float FootLockRelease=.30f;   // m : au-delà, le pied décroche et se repose plus loin
        const float FootLockBlend=12f;      // 1/s : entrée/sortie du verrouillage
        readonly Vector3[] footLockPoint=new Vector3[2];readonly bool[] footLocked=new bool[2];readonly float[] footLockWeight=new float[2];
        void MecanimFootLock(float dt,bool reset)
        {
            for(int i=0;i<2;i++){
                string side=Sides[i];var foot=Limb(side).foot;var p=foot.position;
                if(reset){footLocked[i]=false;footLockWeight[i]=0;continue;}
                bool contact=p.y<FootContactHeight*transform.localScale.y;
                if(contact&&!footLocked[i]){footLocked[i]=true;footLockPoint[i]=p;}
                var drift=footLockPoint[i]-p;drift.y=0;
                if(footLocked[i]&&(!contact||drift.magnitude>FootLockRelease))footLocked[i]=false;
                footLockWeight[i]=Mathf.MoveTowards(footLockWeight[i],footLocked[i]?1:0,dt*FootLockBlend);
                if(footLockWeight[i]<=0)continue;
                var target=Vector3.Lerp(p,new Vector3(footLockPoint[i].x,p.y,footLockPoint[i].z),footLockWeight[i]);var rotation=foot.rotation;
                SolveLeg(side,target);foot.rotation=rotation;
            }
        }
    }
}
