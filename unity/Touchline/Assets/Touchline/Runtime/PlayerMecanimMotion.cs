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
        const string RunClip="Standard Run",SprintClip="Two Cycle Sprint",JogBackClip="Jog Backward",RunBackClip="Run Backward",StrafeLeftClip="Jog Strafe Left",StrafeRightClip="Jog Strafe Right";
        public static bool MecanimReady=>MecanimClips.ContainsKey(IdleClip)&&MecanimClips.ContainsKey(JogClip);

        // Vitesse (m/s) à laquelle le clip de course/conduite est joué à vitesse 1.
        // Vitesses naturelles des boucles (m/s), mesurées sur le modèle Touchline par
        // MecanimPrototypeFilm (recul du pied d'appui par rapport aux hanches).
        const float JogNaturalSpeed=2.3f,DribbleNaturalSpeed=1.8f;
        const float RunNaturalSpeed=3.8f,SprintNaturalSpeed=5.7f;       // mesurées
        const float JogBackNaturalSpeed=2.1f,RunBackNaturalSpeed=2.9f;  // mesurées
        const float StrafeLeftNaturalSpeed=2.9f,StrafeRightNaturalSpeed=2.0f; // m/s, pas chassés (mesurées, colonne naturalX)
        const float DirectionTurnAngle=12f;           // ° : en dessous, le côté du pas chassé ne change pas (évite le scintillement gauche/droite)
        const float BackwardSmoothing=6f;              // 1/s : lissage de la direction de course
        // Entrées du mixeur : déplacements, puis deux emplacements de gestes.
        const int IdleInput=0,JogInput=1,DribbleInput=2,RunInput=3,SprintInput=4,JogBackInput=5,RunBackInput=6,StrafeLeftInput=7,StrafeRightInput=8,ActionInput=9,MixerInputs=11;
        const float LoopRateMin=.7f,LoopRateMax=2.6f; // cadence relative ; au-delà, le verrouillage des pieds absorbe l'écart
        const float MoveBlendFrom=.25f,MoveBlendTo=1.3f;     // m/s : de l'arrêt à la course
        const float ActionFadeIn=.10f,ActionFadeOut=.22f;     // s
        const float LocomotionBlendRate=8f;                  // 1/s : lissage arrêt/course/conduite

        // Instant (s dans le clip) où le pied, la tête ou les mains touchent le ballon.
        // Mesuré sur les clips (vitesse maximale du membre, voir MecanimPrototypeFilm).
        static readonly Dictionary<string,float> ClipContact=new Dictionary<string,float>{
            // Pic de vitesse du pied qui frappe (analyse 60 Hz des clips Mixamo).
            {"Kick Soccerball",.517f},{"Kick Soccerball (1)",.417f},{"Soccer Pass",.433f},{"Chip",.483f},{"Soccer Penalty Kick",.733f},{"Strike Forward Jog",.467f},{"Goalkeeper Drop Kick",2.083f},
            // Pic de vitesse des mains.
            {"Throw In",1.60f},{"Goalkeeper Overhand Throw",1.60f},{"Goalkeeper Pass",1.133f},{"Goalkeeper Diving Save",1.25f},{"Goalkeeper Diving Save (miroir)",1.25f},{"Goalkeeper Catch",.25f},
            // Estimés (pas de membre dominant mesuré) : tête, tacle glissé, chute.
            {"Header",.55f},{"Receive Soccerball",.30f},{"Fallen Idle",0f},{"Standing Up",StandUpSkip},{"Soccer Header",.95f},{"Soccer Tackle",.45f},{"Soccer Trip",.70f},
            // Tacle debout (pied intérieur) : pic de vitesse du pied, comme la passe.
            {"Soccer Pass (miroir)",.433f},
            // Amortis : poitrine juste avant le recul maximal du buste (0,75 s), cuisse au plus haut du genou.
            {"Header Soccerball (1)",.65f},{"Kneeing Soccerball",.283f},{"Kneeing Soccerball (1)",.55f},
        };

        Animator mecanimAnimator;PlayableGraph mecanimGraph;AnimationMixerPlayable mecanimMixer;
        AnimationClipPlayable idlePlayable,jogPlayable,dribblePlayable,runPlayable,sprintPlayable,jogBackPlayable,runBackPlayable;
        readonly AnimationClipPlayable[] actionPlayables=new AnimationClipPlayable[2];
        readonly float[] actionWeights=new float[2];readonly bool[] actionLive=new bool[2];
        int activeAction=-1;string mecanimActionKey;int mecanimActionSequence=-1;float mecanimActionElapsed,mecanimActionContact;
        float moveBlend,dribbleBlend,backwardBlend,sideBlend,sideSign=1,strafeRightBlend=1;bool mecanimKeeper;
        AnimationClipPlayable strafeLeftPlayable,strafeRightPlayable;

        bool EnsureMecanimGraph(bool keeper)
        {
            if(mecanimGraph.IsValid()&&mecanimKeeper==keeper)return true;
            if(!MecanimReady)return false;
            DisposeMecanim();mecanimKeeper=keeper;
            if(mecanimAnimator==null){mecanimAnimator=body.gameObject.GetComponent<Animator>();if(mecanimAnimator==null)mecanimAnimator=body.gameObject.AddComponent<Animator>();}
            mecanimAnimator.avatar=HumanoidAvatar();mecanimAnimator.applyRootMotion=false;mecanimAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            // Courbes de lacet des virages mesurées avant la création du graphe du joueur.
            ClipYawCurve(SprintTurnClip);ClipYawCurve(SprintTurnClip+" (miroir)");turning=false;turnSlot=-1;
            mecanimGraph=PlayableGraph.Create("Touchline player "+PlayerId);mecanimGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output=AnimationPlayableOutput.Create(mecanimGraph,"Body",mecanimAnimator);
            mecanimMixer=AnimationMixerPlayable.Create(mecanimGraph,MixerInputs);output.SetSourcePlayable(mecanimMixer);
            AnimationClipPlayable Loop(string name,string fallback=IdleClip){var clip=MecanimClips.TryGetValue(name,out var c)?c:MecanimClips[fallback];var p=AnimationClipPlayable.Create(mecanimGraph,clip);p.SetApplyFootIK(true);
                // Phase de départ propre à chaque joueur : pas de foulées synchronisées.
                p.SetTime(clip.length*((motionIdentity%97)/97f));return p;}
            idlePlayable=Loop(keeper&&MecanimClips.ContainsKey(KeeperIdleClip)?KeeperIdleClip:IdleClip);jogPlayable=Loop(JogClip);dribblePlayable=Loop(MecanimClips.ContainsKey(DribbleClip)?DribbleClip:JogClip);
            runPlayable=Loop(RunClip,JogClip);sprintPlayable=Loop(SprintClip,RunClip);jogBackPlayable=Loop(JogBackClip,JogClip);runBackPlayable=Loop(RunBackClip,JogBackClip);
            mecanimGraph.Connect(idlePlayable,0,mecanimMixer,IdleInput);mecanimGraph.Connect(jogPlayable,0,mecanimMixer,JogInput);mecanimGraph.Connect(dribblePlayable,0,mecanimMixer,DribbleInput);
            mecanimGraph.Connect(runPlayable,0,mecanimMixer,RunInput);mecanimGraph.Connect(sprintPlayable,0,mecanimMixer,SprintInput);mecanimGraph.Connect(jogBackPlayable,0,mecanimMixer,JogBackInput);mecanimGraph.Connect(runBackPlayable,0,mecanimMixer,RunBackInput);
            strafeLeftPlayable=Loop(StrafeLeftClip,JogClip);strafeRightPlayable=Loop(StrafeRightClip,JogClip);mecanimGraph.Connect(strafeLeftPlayable,0,mecanimMixer,StrafeLeftInput);mecanimGraph.Connect(strafeRightPlayable,0,mecanimMixer,StrafeRightInput);
            for(int i=0;i<2;i++){actionWeights[i]=0;actionLive[i]=false;}activeAction=-1;mecanimActionKey=null;mecanimActionSequence=-1;
            return true;
        }

        void DisposeMecanim(){if(mecanimGraph.IsValid())mecanimGraph.Destroy();}

        // Geste du moteur → (clip, instant de contact côté moteur en s depuis le début du geste).
        bool leftFootedNow;float mecanimPoseSpeed;
        const string RunningStrikeClip="Strike Forward Jog",KeeperThrowClip="Goalkeeper Overhand Throw";
        const float RunningStrikeSpeed=2.5f; // m/s : au-delà, le tireur frappe dans sa course
        const string ReceiveClip="Receive Soccerball",FallenClip="Fallen Idle",StandUpClip="Standing Up";
        const float ReceiveMaxSpeed=.7f;    // m/s : contrôle arrêté seulement (le clip fait des pas sur place)
        const float TripLyingFrom=.9f;      // s après la chute : fin de Soccer Trip, le joueur est au sol
        const float StandUpLead=1.0f;       // s avant la fin de la chute : début du relevé
        const float StandUpSkip=.67f;       // s : début utile de Standing Up (encore allongé avant)
        // Côté (+1 droite du gardien) vers lequel plongent les clips d'origine ; mesuré (colonne lean).
        const float DiveClipSide=1f;
        // Contrôles du corps et tacle debout (choisis sur les rendus de MecanimPrototypeFilm).
        const string ChestControlClip="Header Soccerball (1)";                              // buste qui recule, bras écartés
        const string RightThighClip="Kneeing Soccerball",LeftThighClip="Kneeing Soccerball (1)"; // genou droit / gauche levé
        const string StandingTackleClip="Soccer Pass";                                      // pied droit ; miroir pour le pied gauche
        const float BallSideDeadZone=.06f;  // m : ballon dans l'axe du corps, côté du pied fort
        bool BallOnLeft(Actor actor){float lateral=transform.InverseTransformPoint(new Vector3(actor.actionTarget.x,0,actor.actionTarget.z)).x;return Mathf.Abs(lateral)<BallSideDeadZone?leftFootedNow:lateral<0;}
        bool mecanimPhased; // geste en plusieurs clips (chute → au sol → relevé)
        bool MecanimAction(Actor actor,out string clip,out float simContact)
        {
            clip=null;simContact=0;mecanimPhased=false;
            switch(actor.action){
                case "kick":
                    // Kick Soccerball frappe du pied gauche, Kick Soccerball (1) du droit.
                    // Frappe en pleine course (élan) : Strike Forward Jog, sinon frappe arrêtée selon le pied fort.
                    clip=actor.actionKind=="shot"?(mecanimPoseSpeed>=RunningStrikeSpeed&&MecanimClips.ContainsKey(RunningStrikeClip)?RunningStrikeClip:leftFootedNow?"Kick Soccerball":"Kick Soccerball (1)"):actor.actionKind=="cross"||actor.actionKind=="switch"||actor.actionKind=="clearance"?"Chip":"Soccer Pass";
                    if(actor.slot==0&&MecanimClips.ContainsKey("Goalkeeper Drop Kick")&&actor.actionKind=="clearance")clip="Goalkeeper Drop Kick";
                    simContact=.18f;break;
                case "header":clip="Header";simContact=actor.actionContactTime>0?actor.actionContactTime:.12f;break;
                case "slide":clip="Soccer Tackle";simContact=actor.actionContactTime>0?actor.actionContactTime:.2f;break;
                case "fall":{
                    // Chute de contact : trébuche, reste au sol, se relève avant la fin.
                    float elapsed=MatchSimulation.ContactFallDuration-actor.actionTime;mecanimPhased=true;
                    clip=actor.actionTime<=StandUpLead&&MecanimClips.ContainsKey(StandUpClip)?StandUpClip:elapsed>=TripLyingFrom&&MecanimClips.ContainsKey(FallenClip)?FallenClip:"Soccer Trip";simContact=0;break;}
                case "control":
                    // Amorti de la poitrine (recul du buste, bras écartés) ou de la cuisse (genou levé du côté du ballon).
                    if(actor.actionKind==MatchSimulation.ChestControl){clip=ChestControlClip;simContact=actor.actionContactTime>0?actor.actionContactTime:MatchSimulation.BodyControlContactTime;break;}
                    if(actor.actionKind==MatchSimulation.ThighControl){clip=BallOnLeft(actor)?LeftThighClip:RightThighClip;simContact=actor.actionContactTime>0?actor.actionContactTime:MatchSimulation.BodyControlContactTime;break;}
                    if(mecanimPoseSpeed>ReceiveMaxSpeed)break;
                    clip=ReceiveClip;simContact=0;break;
                case "tackle":
                    // Tacle debout : pied intérieur dans le ballon, du côté où il se trouve.
                    if(actor.actionKind!=MatchSimulation.StandingDuel)break;
                    clip=BallOnLeft(actor)?StandingTackleClip+" (miroir)":StandingTackleClip;simContact=actor.actionContactTime>0?actor.actionContactTime:MatchSimulation.TacklePreparation;break;
                case "throw":clip=actor.slot==0?(MecanimClips.ContainsKey(KeeperThrowClip)?KeeperThrowClip:"Goalkeeper Pass"):"Throw In";simContact=actor.actionContactTime>0?actor.actionContactTime:.5f;break;
                case "dive":clip=(actor.diveSide>=0)==(DiveClipSide>0)?"Goalkeeper Diving Save":"Goalkeeper Diving Save (miroir)";simContact=actor.actionContactTime>0?actor.actionContactTime:.2f;break;
                case "claim":clip="Goalkeeper Catch";simContact=.2f;break;
            }
            return clip!=null&&MecanimClips.ContainsKey(clip);
        }

        // Retourne false si l'animation capturée n'est pas disponible (repli procédural).
        // Exact exponential response keeps reversals continuous at 30, 60 and 120 Hz.
        public static float StrafeBlend(float current,float target,float dt)=>Mathf.Lerp(current,Mathf.Clamp01(target),1-Mathf.Exp(-Mathf.Max(0,dt)*LocomotionBlendRate));
        bool MecanimRender(Actor actor,float dt,bool reset,float poseSpeed,bool carrying)
        {
            if(!UseMecanim||!EnsureMecanimGraph(actor.slot==0))return false;leftFootedNow=leftFooted;mecanimPoseSpeed=poseSpeed;
            if(reset){moveBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(MoveBlendFrom,MoveBlendTo,poseSpeed));dribbleBlend=carrying?1:0;}
            float k=1-Mathf.Exp(-dt*LocomotionBlendRate);
            moveBlend=Mathf.Lerp(moveBlend,Mathf.SmoothStep(0,1,Mathf.InverseLerp(MoveBlendFrom,MoveBlendTo,poseSpeed)),k);
            dribbleBlend=Mathf.Lerp(dribbleBlend,carrying?1:0,k);
            float Rate(float natural)=>Mathf.Clamp(poseSpeed/natural,LoopRateMin,LoopRateMax);
            jogPlayable.SetSpeed(Rate(JogNaturalSpeed));dribblePlayable.SetSpeed(Rate(DribbleNaturalSpeed));runPlayable.SetSpeed(Rate(RunNaturalSpeed));sprintPlayable.SetSpeed(Rate(SprintNaturalSpeed));
            jogBackPlayable.SetSpeed(Rate(JogBackNaturalSpeed));runBackPlayable.SetSpeed(Rate(RunBackNaturalSpeed));strafeLeftPlayable.SetSpeed(Rate(StrafeLeftNaturalSpeed));strafeRightPlayable.SetSpeed(Rate(StrafeRightNaturalSpeed));
            // Direction de course par rapport au regard : avant, arrière (repli) ou de côté
            // (pas chassés d'un défenseur qui reste face au jeu). Poids = cos² / sin².
            var travel=transform.InverseTransformDirection(new Vector3(poseVelocity.x,0,poseVelocity.z));travel.y=0;
            float backward=0,side=0;
            if(poseSpeed>=MoveBlendFrom&&travel.sqrMagnitude>1e-6f){
                travel.Normalize();backward=travel.z<0?travel.z*travel.z:0;side=travel.x*travel.x;
                if(Mathf.Abs(travel.x)>Mathf.Sin(DirectionTurnAngle*Mathf.Deg2Rad))sideSign=Mathf.Sign(travel.x);
            }
            float directionK=reset?1:1-Mathf.Exp(-dt*BackwardSmoothing);
            backwardBlend=Mathf.Lerp(backwardBlend,backward,directionK);sideBlend=Mathf.Lerp(sideBlend,side,directionK);
            strafeRightBlend=reset?(sideSign>0?1:0):StrafeBlend(strafeRightBlend,sideSign>0?1:0,dt);

            // Gestes : nouveau geste → emplacement libre, fondu d'entrée, temps calé sur le contact.
            bool acting=MecanimAction(actor,out var clipName,out var simContact),turnGesture=false;
            if(!acting)acting=turnGesture=MecanimSharpTurn(actor,poseSpeed,carrying,out clipName);else turning=false;
            // Un geste garde le clip choisi à son début, même si la vitesse change ensuite.
            if(acting&&!turnGesture&&!mecanimPhased&&mecanimActionKey!=null&&actor.actionSequence==mecanimActionSequence)clipName=mecanimActionKey;
            string key=acting?clipName:null;
            if(acting&&(key!=mecanimActionKey||!turnGesture&&actor.actionSequence!=mecanimActionSequence)){
                int slot=activeAction<0?0:1-activeAction;
                if(actionPlayables[slot].IsValid()){mecanimGraph.Disconnect(mecanimMixer,ActionInput+slot);actionPlayables[slot].Destroy();}
                var clip=MecanimClips[clipName];actionPlayables[slot]=AnimationClipPlayable.Create(mecanimGraph,clip);actionPlayables[slot].SetApplyFootIK(true);
                mecanimGraph.Connect(actionPlayables[slot],0,mecanimMixer,ActionInput+slot);actionLive[slot]=true;if(activeAction>=0)actionLive[activeAction]=false;
                activeAction=slot;mecanimActionKey=key;mecanimActionSequence=actor.actionSequence;mecanimActionElapsed=0;
                mecanimActionContact=(ClipContact.TryGetValue(clipName,out var c)?c:clip.length*.4f)-simContact;
                if(turnGesture)mecanimActionContact=TurnClipStart(clipName);
                if(turnSlot==slot)turnSlot=-1;if(turnGesture)turnSlot=slot;
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
                if(!actionLive[i]&&actionWeights[i]<=0){mecanimGraph.Disconnect(mecanimMixer,ActionInput+i);actionPlayables[i].Destroy();if(activeAction==i)activeAction=-1;if(turnSlot==i)turnSlot=-1;}
            }
            float a0=actionPlayables[0].IsValid()?actionWeights[0]:0,a1=actionPlayables[1].IsValid()?actionWeights[1]:0;
            // Deux gestes en fondu enchaîné : leur somme ne dépasse pas 1.
            if(a0+a1>1){float scale=1/(a0+a1);a0*=scale;a1*=scale;}
            float turnWeight=turnSlot==0?a0:turnSlot==1?a1:0;
            float locomotion=1-(a0+a1);
            // Allure avant : trot → course → sprint selon la vitesse, entre leurs vitesses naturelles.
            float toRun=Mathf.SmoothStep(0,1,Mathf.InverseLerp(JogNaturalSpeed,RunNaturalSpeed,poseSpeed)),toSprint=Mathf.SmoothStep(0,1,Mathf.InverseLerp(RunNaturalSpeed,SprintNaturalSpeed,poseSpeed));
            float toRunBack=Mathf.SmoothStep(0,1,Mathf.InverseLerp(JogBackNaturalSpeed,RunBackNaturalSpeed,poseSpeed));
            float moving=locomotion*moveBlend,free=moving*(1-dribbleBlend);
            float sideShare=Mathf.Clamp01(sideBlend),backShare=Mathf.Min(Mathf.Clamp01(backwardBlend),1-sideShare);
            float forward=free*(1-sideShare-backShare),back=free*backShare,lateral=free*sideShare;
            mecanimMixer.SetInputWeight(IdleInput,locomotion*(1-moveBlend));
            mecanimMixer.SetInputWeight(JogInput,forward*(1-toRun));
            mecanimMixer.SetInputWeight(RunInput,forward*toRun*(1-toSprint));
            mecanimMixer.SetInputWeight(SprintInput,forward*toRun*toSprint);
            mecanimMixer.SetInputWeight(JogBackInput,back*(1-toRunBack));
            mecanimMixer.SetInputWeight(RunBackInput,back*toRunBack);
            mecanimMixer.SetInputWeight(StrafeLeftInput,lateral*(1-strafeRightBlend));mecanimMixer.SetInputWeight(StrafeRightInput,lateral*strafeRightBlend);
            mecanimMixer.SetInputWeight(DribbleInput,moving*dribbleBlend);
            mecanimMixer.SetInputWeight(ActionInput,a0);mecanimMixer.SetInputWeight(ActionInput+1,a1);
            body.localPosition=Vector3.zero;body.localRotation=Quaternion.identity;
            mecanimGraph.Evaluate(reset?0:Mathf.Max(0,dt));
            if(turnWeight>0)MecanimTurnYaw(turnWeight);
            MecanimFootLock(dt,reset);
            return true;
        }

        // Virage serré en course : au lieu de pivoter tout le corps d'un bloc pendant que
        // les jambes continuent leur foulée, le joueur plante un appui et repart dans la
        // nouvelle direction (« Sprint Turn », 60° à droite ; miroir à gauche). Le lacet
        // visible suit celui du clip, mis à l'échelle de l'angle réel du virage, puis
        // rejoint l'orientation de présentation au fondu de sortie (toujours interpolé).
        const string SprintTurnClip="Sprint Turn";
        const float SprintTurnClipSide=1f;          // +1 : le clip d'origine tourne vers la droite (lacet mesuré +60°)
        const float SharpTurnMinSpeed=2.5f;         // m/s : en dessous, le pivot ordinaire suffit
        const float SharpTurnMinAngle=40f,SharpTurnMaxAngle=120f; // ° d'écart entre le regard et la nouvelle direction
        const float SharpTurnMaxBackward=.3f,SharpTurnMaxSide=.4f; // part de course arrière / de côté tolérée au déclenchement
        const float SharpTurnMaxScale=2f;           // facteur maximal appliqué au lacet du clip
        const float TurnOnsetYaw=4f;                // ° : début de la rotation dans le clip
        const float TurnLead=.10f;                  // s de clip joués avant le début de la rotation (appui)
        const float YawCurveRate=30f;               // échantillons/s de la courbe de lacet d'un clip
        static readonly Dictionary<string,float[]> clipYawCurves=new Dictionary<string,float[]>();
        float mecanimFacingTarget;                  // ° : orientation de présentation visée (PlayerView)
        bool turning;float turnFrom;int turnSlot=-1;
        // Clip du geste capturé en cours (null : déplacement seul) ; pour les films de contrôle.
        public string MecanimGestureClip=>mecanimActionKey;

        bool MecanimSharpTurn(Actor actor,float poseSpeed,bool carrying,out string clip)
        {
            clip=null;
            if(actor.action!="run"||carrying){turning=false;return false;}
            if(turning){
                // Le virage dure jusqu'à la fin du clip, fondu de sortie compris.
                if(activeAction>=0&&activeAction==turnSlot&&actionLive[activeAction]&&mecanimActionKey!=null&&mecanimActionContact+mecanimActionElapsed<MecanimClips[mecanimActionKey].length-ActionFadeOut){clip=mecanimActionKey;return true;}
                turning=false;return false;
            }
            float delta=Mathf.DeltaAngle(transform.eulerAngles.y,mecanimFacingTarget);
            if(poseSpeed<SharpTurnMinSpeed||Mathf.Abs(delta)<SharpTurnMinAngle||Mathf.Abs(delta)>SharpTurnMaxAngle||backwardBlend>SharpTurnMaxBackward||sideBlend>SharpTurnMaxSide)return false;
            clip=(delta>=0)==(SprintTurnClipSide>0)?SprintTurnClip:SprintTurnClip+" (miroir)";
            if(!MecanimClips.ContainsKey(clip)||ClipYawCurve(clip)==null){clip=null;return false;}
            turning=true;turnFrom=transform.eulerAngles.y;return true;
        }

        // Instant du clip de virage où il commence (un peu avant le début de sa rotation).
        float TurnClipStart(string name)
        {
            var curve=ClipYawCurve(name);if(curve==null)return 0;
            for(int k=0;k<curve.Length;k++)if(Mathf.Abs(curve[k])>TurnOnsetYaw)return Mathf.Max(0,k/YawCurveRate-TurnLead);
            return 0;
        }

        void MecanimTurnYaw(float weight)
        {
            var playable=actionPlayables[turnSlot];if(!playable.IsValid())return;
            var curve=ClipYawCurve(playable.GetAnimationClip().name);if(curve==null)return;
            float u=Mathf.Clamp((float)playable.GetTime()*YawCurveRate,0,curve.Length-1);int k=Mathf.Min((int)u,curve.Length-2);
            float clipYaw=Mathf.Lerp(curve[k],curve[k+1],u-k),clipTotal=curve[curve.Length-1];
            if(Mathf.Abs(clipTotal)<1)return;
            float scale=Mathf.Clamp(Mathf.DeltaAngle(turnFrom,mecanimFacingTarget)/clipTotal,0,SharpTurnMaxScale);
            // Lacet visible = mélange (poids du geste) entre l'orientation de présentation et le virage du clip.
            float offset=weight*(Mathf.DeltaAngle(transform.eulerAngles.y,turnFrom+scale*clipYaw)-clipYaw);
            body.localRotation=Quaternion.Euler(0,offset,0);
        }

        // Lacet du bassin (°, relatif au début du clip) échantillonné une fois par clip.
        float[] ClipYawCurve(string name)
        {
            if(clipYawCurves.TryGetValue(name,out var curve))return curve;
            curve=null;
            if(mecanimAnimator!=null&&MecanimClips.TryGetValue(name,out var clip)){
                var graph=PlayableGraph.Create("Touchline yaw "+name);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output=AnimationPlayableOutput.Create(graph,"Yaw",mecanimAnimator);var playable=AnimationClipPlayable.Create(graph,clip);output.SetSourcePlayable(playable);
                var left=mecanimAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var right=mecanimAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                if(left!=null&&right!=null){
                    int count=Mathf.Max(2,Mathf.CeilToInt(clip.length*YawCurveRate)+1);curve=new float[count];float previous=0;
                    for(int k=0;k<count;k++){
                        playable.SetTime(Mathf.Min(k/YawCurveRate,clip.length));graph.Evaluate(0);
                        var facing=Vector3.Cross(body.InverseTransformDirection(right.position-left.position),Vector3.up);float yaw=Mathf.Atan2(facing.x,facing.z)*Mathf.Rad2Deg;
                        curve[k]=k==0?0:curve[k-1]+Mathf.DeltaAngle(previous,yaw);previous=yaw;
                    }
                }
                graph.Destroy();
            }
            clipYawCurves[name]=curve;return curve;
        }

        // Verrouillage des pieds : un pied qui touche le sol reste à sa place sur la
        // pelouse pendant l'appui (la jambe s'ajuste par IK) au lieu de glisser
        // quand la foulée du clip ne correspond pas exactement à la vitesse réelle.
        // Un pied posé trop loin de sa place dans la pose (corps qui pivote pendant un
        // contrôle, clip qui piétine sur place) ne glisse pas pour la rejoindre : il se
        // lève et se repose par un petit pas.
        const float FootContactHeight=.12f; // m (cheville), joueur de 1,82 m
        const float FootStepTrigger=.20f;   // m : écart pied posé / pose qui déclenche un pas de replacement
        const float FootLockRelease=.40f;   // m : écart maximal, même si l'autre pied est déjà en train de se replacer
        const float FootStepDuration=.18f;  // s : durée d'un pas de replacement
        const float FootStepLift=.09f;      // m : hauteur du pied au milieu du pas
        const float FootLockBlend=12f;      // 1/s : sortie du verrouillage quand la pose lève le pied
        readonly Vector3[] footLockPoint=new Vector3[2];readonly bool[] footLocked=new bool[2];readonly float[] footLockWeight=new float[2];
        readonly float[] footStep=new float[2];readonly Vector3[] footStepFrom=new Vector3[2];
        void MecanimFootLock(float dt,bool reset)
        {
            for(int i=0;i<2;i++){
                string side=Sides[i];var foot=Limb(side).foot;var p=foot.position;
                if(reset){footLocked[i]=false;footLockWeight[i]=0;footStep[i]=0;continue;}
                bool contact=p.y<FootContactHeight*transform.localScale.y;
                if(footStep[i]>0){
                    // Pas de replacement : du point posé vers la place du pied dans la pose, en arc.
                    footStep[i]=Mathf.Min(1,footStep[i]+dt/FootStepDuration);float eased=Mathf.SmoothStep(0,1,footStep[i]);
                    var stepTarget=Vector3.Lerp(new Vector3(footStepFrom[i].x,p.y,footStepFrom[i].z),p,eased);stepTarget.y=p.y+Mathf.Sin(footStep[i]*Mathf.PI)*FootStepLift*transform.localScale.y;
                    if(footStep[i]>=1){footStep[i]=0;footLocked[i]=contact;footLockPoint[i]=p;footLockWeight[i]=contact?1:0;}
                    var stepRotation=foot.rotation;SolveLeg(side,stepTarget);foot.rotation=stepRotation;continue;
                }
                // Verrouillage immédiat sur la position affichée du pied (pas de saut, pas de glissement d'entrée).
                if(contact&&!footLocked[i]){var shown=Vector3.Lerp(p,footLockPoint[i],footLockWeight[i]);footLocked[i]=true;footLockPoint[i]=new Vector3(shown.x,p.y,shown.z);footLockWeight[i]=1;}
                var drift=footLockPoint[i]-p;drift.y=0;
                if(footLocked[i]&&contact&&(drift.magnitude>FootLockRelease||drift.magnitude>FootStepTrigger&&footStep[1-i]<=0)){
                    // The solver may not reach an old anchor; start from the last visible foot, not that unreachable point.
                    footStepFrom[i]=previousFeet[i];footStep[i]=Mathf.Epsilon;footLocked[i]=false;footLockWeight[i]=0;
                    var holdRotation=foot.rotation;SolveLeg(side,new Vector3(footStepFrom[i].x,p.y,footStepFrom[i].z));foot.rotation=holdRotation;continue;
                }
                if(footLocked[i]&&!contact)footLocked[i]=false;
                footLockWeight[i]=Mathf.MoveTowards(footLockWeight[i],footLocked[i]?1:0,dt*FootLockBlend);
                if(footLockWeight[i]<=0)continue;
                var target=Vector3.Lerp(p,new Vector3(footLockPoint[i].x,p.y,footLockPoint[i].z),footLockWeight[i]);var rotation=foot.rotation;
                SolveLeg(side,target);foot.rotation=rotation;
            }
        }
    }
}
