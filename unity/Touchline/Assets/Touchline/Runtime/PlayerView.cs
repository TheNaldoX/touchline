using System;
using System.Collections.Generic;
using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    [Serializable] public class HumanPart { public int material; public float[] position,normal,uv,skinWeight; public int[] skinIndex,index; }
    [Serializable] public class HumanBone { public string name; public int parent; public float[] head,tail; }
    [Serializable] public class HumanSource { public float height; public HumanPart[] parts; public HumanBone[] bones; }
    public sealed partial class PlayerView : MonoBehaviour
    {
        public string PlayerId { get; private set; }
        public float MotionStature {get;private set;}=1.8f;
        public Vector3 LabelHeadPosition=>bones.TryGetValue("head",out var head)?head.position:transform.position+Vector3.up*1.8f;
        readonly Dictionary<string,Transform> bones=new Dictionary<string,Transform>();
        Transform body;Transform[] skeleton;Material shirt;Material[] ownedMaterials;
        readonly Vector3[] planted=new Vector3[2],swingFrom=new Vector3[2],previousFeet=new Vector3[2];
        readonly bool[] stance=new bool[2];
        // Pas d'ajustement à l'arrêt : un pied trop loin de sa position de repos
        // (pivot, replacement) est déplacé par un petit pas, l'autre reste posé.
        readonly float[] adjustStep=new float[2];readonly Vector3[] adjustFrom=new Vector3[2];
        const float AdjustStepTrigger=.14f,AdjustStepDuration=.24f,AdjustStepLift=.07f; // m, s, m
        readonly Quaternion[] previousFootRotations=new Quaternion[2];
        readonly LocomotionFacing locomotionFacing=new LocomotionFacing();
        // Trajectoire lissée entre les pas de simulation (vitesse continue) ; poseVelocity
        // pilote l'allure du corps (cadence, mélange marche/course) sans paliers à 10 Hz.
        readonly MotionCurve rootCurve=new MotionCurve();Point poseVelocity;
        const float ActionTurnRate=600f; // °/s : plafond de rotation du corps pendant un geste (contrôle, frappe, duel)
        float gait,lean;bool initialized,leftFooted;Vector3 lastPosition;Quaternion lastRotation;
        static HumanSource source;
        static Mesh geometry;
        static readonly string[] Sides={"L","R"};
        Quaternion[] previousPose;
        Quaternion[] transitionPose;Vector3 transitionBodyPosition;Quaternion transitionBodyRotation;
        int poseSequence;string poseAction;float transitionRemaining,transitionDuration;bool receivingLeft;
        public static Vector3 Vector(float[] a)=>new Vector3(a[0],a[1],a[2]);
        public void Build(PlayerData player,int side,int slot,Color team)=>Build(player,side,slot,MatchKit.Plain(team));
        public void Build(PlayerData player,int side,int slot,MatchKit matchKit)
        {
            kit=matchKit??MatchKit.Plain(Color.white);var team=kit.shirt;
            PlayerId=player.id;leftFooted=player.preferredFoot=="Left"||player.preferredFoot=="Gauche";if(source==null)source=JsonUtility.FromJson<HumanSource>(Resources.Load<TextAsset>("Models/footballer").text);
            body=new GameObject("Rig").transform;body.SetParent(transform,false);skeleton=new Transform[source.bones.Length];
            for(int i=0;i<skeleton.Length;i++){var b=source.bones[i];var bone=new GameObject(b.name).transform;bone.SetParent(body,false);bone.localPosition=Vector(b.head);skeleton[i]=bone;bones[b.name]=bone;if(b.name=="wrist.L"||b.name=="wrist.R")handAxes[b.name=="wrist.L"?0:1]=Vector(b.tail)-Vector(b.head);}
            for(int i=0;i<skeleton.Length;i++){var parent=source.bones[i].parent;if(parent>=0)skeleton[i].SetParent(skeleton[parent],true);}
            uint identity=AppearanceIdentity(player.id);motionIdentity=(int)(identity%10000)+1;var skin=Color.Lerp(new Color(.35f,.23f,.17f),new Color(.85f,.68f,.54f),(identity%101)/100f);previousPose=new Quaternion[skeleton.Length];transitionPose=new Quaternion[skeleton.Length];
            shirt=Material(slot==0?new Color(.85f,.65f,.12f):team);var mats=new[]{Material(skin),shirt,Material(team*.58f),Material(team*.8f),Material(new Color(.075f,.055f,.04f)),Material(new Color(.045f,.055f,.065f)),Material(Color.white),Material(Color.white),PrintMaterial()};
            ApplyAppearance(mats,identity);
            ownedMaterials=mats;
            if(geometry==null)geometry=BuildGeometry();
            var anatomy=new GameObject("Skinned anatomy");anatomy.transform.SetParent(body,false);var renderer=anatomy.AddComponent<SkinnedMeshRenderer>();bodyRenderer=renderer;renderer.sharedMesh=AnatomyMesh(identity);renderer.bones=skeleton;renderer.rootBone=body;
            var sections=new Material[source.parts.Length+1];for(int i=0;i<source.parts.Length;i++)sections[i]=mats[Mathf.Clamp(source.parts[i].material,0,mats.Length-1)];sections[source.parts.Length]=mats[PrintSlot];renderer.sharedMaterials=sections;
            renderer.localBounds=new Bounds(new Vector3(0,.9f,0),new Vector3(4.6f,3.8f,3.6f));renderer.updateWhenOffscreen=false;
            foreach(var sideName in Sides)if(bones.TryGetValue("foot."+sideName,out var foot)){var boot=new GameObject("Football boot");boot.transform.SetParent(foot,false);boot.AddComponent<MeshFilter>().sharedMesh=FootballBootMesh.Shared;boot.AddComponent<MeshRenderer>().sharedMaterial=mats[5];}
            var pick=gameObject.AddComponent<CapsuleCollider>();pick.height=1.85f;pick.radius=.36f;pick.center=new Vector3(0,.92f,0);
            PrepareLimbs();PrepareHands();ApplyPhysique(player);SetPrintIdentity(player);SetKeeperAppearance(slot==0);
        }
        Mesh BuildGeometry()
        {
            int count=0;foreach(var part in source.parts)count+=part.position.Length/3;
            ShirtPrint.Patch(ShirtPart,out var printPositions,out var printNormals,out var printUv,out var printWeights,out var printTriangles);int printStart=count;count+=printPositions.Length;
            var mesh=new Mesh{name="Footballer anatomy",indexFormat=count<65536?UnityEngine.Rendering.IndexFormat.UInt16:UnityEngine.Rendering.IndexFormat.UInt32};
            var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];bool hasNormals=true;var weights=new BoneWeight[count];var indices=new int[source.parts.Length+1][];int offset=0;
            for(int i=0;i<source.parts.Length;i++){
                var p=source.parts[i];int length=p.position.Length/3;
                for(int v=0;v<length;v++){vertices[offset+v]=new Vector3(p.position[v*3],p.position[v*3+1],p.position[v*3+2]);weights[offset+v]=new BoneWeight{boneIndex0=p.skinIndex[v*4],boneIndex1=p.skinIndex[v*4+1],boneIndex2=p.skinIndex[v*4+2],boneIndex3=p.skinIndex[v*4+3],weight0=p.skinWeight[v*4],weight1=p.skinWeight[v*4+1],weight2=p.skinWeight[v*4+2],weight3=p.skinWeight[v*4+3]};}
                hasNormals&=p.normal?.Length==length*3;
                for(int v=0;v<length;v++){if(p.normal?.Length==length*3)normals[offset+v]=new Vector3(p.normal[v*3],p.normal[v*3+1],p.normal[v*3+2]);if(p.uv?.Length==length*2)uv[offset+v]=new Vector2(p.uv[v*2],p.uv[v*2+1]);}
                if(p.material==HairMaterialIndex){hairStart=offset;hairCount=length;}
                indices[i]=new int[p.index.Length];for(int j=0;j<p.index.Length;j++)indices[i][j]=p.index[j]+offset;offset+=length;
            }
            // Dernier sous-maillage : flocage du dos (voir ShirtPrint).
            for(int v=0;v<printPositions.Length;v++){vertices[printStart+v]=printPositions[v];normals[printStart+v]=printNormals[v];uv[printStart+v]=printUv[v];weights[printStart+v]=printWeights[v];}
            indices[source.parts.Length]=new int[printTriangles.Length];for(int j=0;j<printTriangles.Length;j++)indices[source.parts.Length][j]=printTriangles[j]+printStart;
            var bind=new Matrix4x4[skeleton.Length];for(int i=0;i<bind.Length;i++)bind[i]=skeleton[i].worldToLocalMatrix*body.localToWorldMatrix;
            mesh.vertices=vertices;mesh.boneWeights=weights;mesh.bindposes=bind;mesh.subMeshCount=indices.Length;for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);
            mesh.uv=uv;if(hasNormals)mesh.normals=normals;else mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void ApplyPhysique(PlayerData data){MotionStature=data.heightCm>=145&&data.heightCm<=215?data.heightCm*.01f:1.8f;float scale=data.heightCm>=145&&data.heightCm<=215?(data.heightCm*.01f)/source.height:1;transform.localScale=Vector3.one*scale;}
        public void ChangeIdentity(PlayerData data){PlayerId=data.id;leftFooted=data.preferredFoot=="Left"||data.preferredFoot=="Gauche";ApplyPhysique(data);uint identity=AppearanceIdentity(data.id);motionIdentity=(int)(identity%10000)+1;ApplyAppearance(ownedMaterials,identity);if(bodyRenderer!=null)bodyRenderer.sharedMesh=AnatomyMesh(identity);SetPrintIdentity(data);initialized=false;}
        public Vector3 FootPosition(bool left)=>Limb((left?"L":"R")).foot.position;
        public void ResetPresentation(){initialized=false;}
        public Vector3 BootContactPosition(bool left){var foot=Limb(left?"L":"R").foot;return foot.TransformPoint(new Vector3(0,0,.22f))+foot.forward*.11f;}
        Vector3 BootTarget(Vector3 ball){var target=ball-transform.forward*(.22f*transform.localScale.y+.11f);target.y=Mathf.Max(.08f,ball.y);return target;}
        public Vector3 HeldBallPosition=>(bones["wrist.L"].position+bones["wrist.R"].position)*.5f+gripOffset+transform.forward*.06f;
        static bool DefensiveReadinessIntent(string intent)=>intent=="press"||intent=="mark"||intent=="mark-carrier"||intent=="cover";
        public void Render(Actor actor,float alpha,float dt,Vector3 ballPosition=default,PlayerMotionContext context=default)
        {
            if(keeperAppearance!=(actor.slot==0))SetKeeperAppearance(actor.slot==0);
            ShirtPrint.Flush();
            // Interpolate action progress on the same fixed-step timeline as
            // positions. Otherwise procedural contacts update only at 10 Hz.
            float actionTime=actor.actionTime+Mathf.Clamp01(1-alpha)*MatchSimulation.Step;
            gripOffset=Vector3.zero;
            if(initialized)for(int i=0;i<2;i++){previousFeet[i]=Limb(Sides[i]).foot.position;previousFootRotations[i]=Limb(Sides[i]).foot.rotation;}
            rootCurve.Observe(actor.previous,actor.position,MatchSimulation.Step);
            var p=rootCurve.Position(actor.previous,actor.position,MatchSimulation.Step,alpha);transform.position=new Vector3(p.x,0,p.z);
            poseVelocity=rootCurve.Velocity(actor.previous,actor.position,MatchSimulation.Step,alpha);float poseSpeed=poseVelocity.Length;
            float travel=Vector3.Distance(transform.position,lastPosition);bool reset=!initialized||travel>2.5f;
            bool wall=actor.intent=="wall"&&actor.velocity.Length<.5f;float facing=wall?Mathf.Atan2(ballPosition.x-p.x,ballPosition.z-p.z):actor.angle;
            bool defend=actor.slot>0&&context.defending&&!context.carrying&&(actor.action=="run"||actor.action=="idle")&&(actor.intent=="shape"||DefensiveReadinessIntent(actor.intent)||actor.intent=="recover"||actor.intent=="delay");
            var ballOffset=new Vector2(ballPosition.x-p.x,ballPosition.z-p.z);
            if(defend&&ballOffset.sqrMagnitude>.01f){
                // A defender tracks the ball while shuffling or retreating.
                // At sprint speed the torso progressively follows the run.
                float watch=(1-Mathf.SmoothStep(0,1,(actor.velocity.Length-3.2f)/2.6f))*(1-Mathf.SmoothStep(0,1,(ballOffset.magnitude-14)/6));
                facing=Mathf.LerpAngle(actor.angle*Mathf.Rad2Deg,Mathf.Atan2(ballOffset.x,ballOffset.y)*Mathf.Rad2Deg,watch)*Mathf.Deg2Rad;
            }
            mecanimFacingTarget=facing*Mathf.Rad2Deg;
            if(actor.action=="run"||actor.action=="idle"||MatchSimulation.PreparingFootDelivery(actor)||(actor.action=="keeper-hold"&&actor.actionKind==MatchSimulation.KeeperDistributionTurn)||ContinueHeaderFacing(actor,actionTime,dt,reset,facing))
                transform.rotation=Quaternion.Euler(0,locomotionFacing.Sample(facing*Mathf.Rad2Deg,actor.velocity.Length,dt,reset),0);
            else {
                // Contact actions retain their existing orientation timing.
                // Same convergence, but never faster than a real quick turn: no
                // half-turn snapped in two frames when an action starts.
                var actionFacing=Quaternion.Euler(0,facing*Mathf.Rad2Deg,0);
                float turnStep=Mathf.Min(Quaternion.Angle(transform.rotation,actionFacing)*(1-Mathf.Exp(-dt*16)),ActionTurnRate*dt);
                transform.rotation=reset?actionFacing:Quaternion.RotateTowards(transform.rotation,actionFacing,turnStep);
                locomotionFacing.Sample(transform.eulerAngles.y,actor.velocity.Length,0,true);
            }
            float run=Mathf.Clamp01(poseSpeed/7);
            var travelDirection=poseSpeed>.2f?new Vector3(poseVelocity.x,0,poseVelocity.z).normalized:transform.forward;
            // Animation par mouvements capturés (Mecanim) : remplace la pose procédurale.
            if(UseMecanim&&MecanimRender(actor,dt,reset,poseSpeed,context.carrying)){MecanimClaimReadiness(actor,context.keeperClaim,actionTime,dt,reset,ballPosition);MecanimKeeperPlacement(actor,actionTime,ballPosition,dt,reset);poseAction=actor.action;poseSequence=actor.actionSequence;lastPosition=transform.position;lastRotation=transform.rotation;initialized=true;return;}
            var localTravel=transform.InverseTransformDirection(travelDirection);
            var previousBodyPosition=body.localPosition;var previousBodyRotation=body.localRotation;
            if(actor.action=="control"&&(reset||poseAction!="control"||poseSequence!=actor.actionSequence)){var contact=MatchSimulation.IsBodyControl(actor)?new Vector3(actor.actionTarget.x,actor.actionHeight,actor.actionTarget.z):ballPosition;float lateral=transform.InverseTransformPoint(contact).x;receivingLeft=Mathf.Abs(lateral)<.06f?leftFooted:lateral>0;}
            bool actionChanged=poseAction!=actor.action||poseSequence!=actor.actionSequence;
            PrepareGroundEntry(actor,reset,actionChanged);
            UpdateBodyReceptionPreparation(actor,dt,reset,context.bodyReception);
            poseSequence=actor.actionSequence;
            if(!reset&&actionChanged){transitionDuration=poseAction=="dive"||poseAction=="tackle"?.20f:.10f;transitionRemaining=transitionDuration;transitionBodyPosition=previousBodyPosition;transitionBodyRotation=previousBodyRotation;for(int i=0;i<skeleton.Length;i++)transitionPose[i]=skeleton[i].localRotation;}
            poseAction=actor.action;
            if(reset){transitionRemaining=0;lean=0;gait=0;}
            if(!reset)gait+=travel/((FullBodyMotion.Available?DirectionalBodyMotion.Stride(poseSpeed,localTravel,motionIdentity,actor.injured):Mathf.Lerp(1.2f,3.15f,run))*transform.localScale.y);
            float turn=reset?0:Mathf.DeltaAngle(lastRotation.eulerAngles.y,transform.eulerAngles.y)/Mathf.Max(.01f,dt);
            lean=Mathf.Lerp(lean,Mathf.Clamp(-turn*.035f,-9,9)*run,1-Mathf.Exp(-dt*8));
            for(int i=0;i<skeleton.Length;i++){previousPose[i]=skeleton[i].localRotation;skeleton[i].localRotation=Quaternion.identity;}
            float cycle=gait*Mathf.PI*2;
            var captured=CapturedLocomotion.Sample(gait,poseSpeed);
            body.localPosition=new Vector3(0,-.025f+Mathf.Abs(Mathf.Sin(cycle*2))*.018f*run,0);body.localRotation=Quaternion.Euler(run*5*localTravel.z,Mathf.Sin(cycle)*3*run,lean-run*3*localTravel.x);
            Rotate("spine02",0,-Mathf.Sin(cycle)*5*run,-lean*.35f);
            var look=transform.InverseTransformDirection(ballPosition-transform.position);Rotate("head",Mathf.Clamp(-look.y*4,-12,12),Mathf.Clamp(Mathf.Atan2(look.x,look.z)*Mathf.Rad2Deg,-45,45)*.6f,0);
            for(int i=0;i<2;i++){
                var side=Sides[i];float sign=i==0?1:-1,phase=Mathf.Repeat(gait+i*.5f,1),contact=Mathf.Lerp(.65f,.43f,run);
                float arm=CapturedLocomotion.Available?(i==0?captured.leftArm:captured.rightArm):-Mathf.Sin(cycle+i*Mathf.PI)*24;float elbow=CapturedLocomotion.Available?(i==0?captured.leftElbow:captured.rightElbow):35-run*25;
                Rotate(Limb(side).upperArm,arm*run,-sign*8,-sign*25);Rotate(Limb(side).lowerArm,Mathf.Lerp(25,elbow,run),0,0);
                var rest=transform.TransformPoint(new Vector3(sign*.16f,.08f,0));
                if(reset){planted[i]=swingFrom[i]=rest;stance[i]=phase<contact;}
                bool onGround=phase<contact||run<.035f;
                if(onGround&&!stance[i]&&!reset)planted[i]=previousFeet[i];
                if(!onGround&&stance[i])swingFrom[i]=planted[i];
                planted[i].y=.08f;stance[i]=onGround;
                Vector3 target;
                if(onGround){target=planted[i];if(run<.035f){
                    if(reset){adjustStep[i]=0;planted[i]=rest;}
                    else if(adjustStep[i]>0){
                        adjustStep[i]=Mathf.Min(1,adjustStep[i]+dt/AdjustStepDuration);float eased=Mathf.SmoothStep(0,1,adjustStep[i]);
                        planted[i]=Vector3.Lerp(adjustFrom[i],rest,eased);target=planted[i];target.y=.08f+Mathf.Sin(adjustStep[i]*Mathf.PI)*AdjustStepLift;
                        if(adjustStep[i]>=1){adjustStep[i]=0;planted[i]=rest;}
                    }
                    else if(adjustStep[1-i]<=0&&Vector3.Distance(new Vector3(planted[i].x,0,planted[i].z),new Vector3(rest.x,0,rest.z))>AdjustStepTrigger){adjustStep[i]=.0001f;adjustFrom[i]=planted[i];}
                    if(adjustStep[i]<=0)target=planted[i];
                }else adjustStep[i]=0;}
                else{float swing=(phase-contact)/(1-contact);var end=rest+travelDirection*Mathf.Lerp(.18f,.62f,run);target=Vector3.Lerp(swingFrom[i],end,Mathf.SmoothStep(0,1,swing));float lift=CapturedLocomotion.Available?Mathf.Lerp(.09f,Mathf.Max(.1f,i==0?captured.leftLift:captured.rightLift),run):Mathf.Lerp(.09f,.25f,run);target.y=.08f+Mathf.Sin(swing*Mathf.PI)*lift;}
                // Keep a planted foot within the anatomical reach during tight turns.
                var relative=target-rest;if(relative.magnitude>.68f)target=rest+relative.normalized*.68f;
                if(actor.action=="kick"&&(i==0)==leftFooted){float elapsed=Mathf.Max(0,.64f-actionTime);float z=elapsed<.18f?Mathf.Lerp(-.30f,.45f,elapsed/.18f):Mathf.Lerp(.45f,.04f,(elapsed-.18f)/.46f);target=transform.TransformPoint(new Vector3(sign*.16f,.12f+Mathf.Sin(Mathf.Clamp01(elapsed/.64f)*Mathf.PI)*.14f,z));}
                if(actor.action=="miscontrol"&&(i==0)==leftFooted)target=transform.TransformPoint(new Vector3(sign*.20f,.12f,.28f+.22f*Mathf.Sin(Mathf.Clamp01(1-actionTime/.4f)*Mathf.PI)));
                SolveLeg(side,target);Limb(side).foot.rotation=transform.rotation;
            }
            if(FullBodyMotion.Available)CapturedBody(actor,alpha,dt,reset,actionChanged);
            ContextBody(actor,dt,reset,ballPosition,context);
            if(FullBodyMotion.Available)FinishCapturedSupportRelease();
            DuelAndInjuryPose(actor,actionTime,reset||actionChanged);
            if(wall){Rotate("spine02",5,0,0);SolveArm("L",transform.TransformPoint(new Vector3(.07f,.94f,.26f)));SolveArm("R",transform.TransformPoint(new Vector3(-.07f,.99f,.28f)));}
            if(actor.action=="header")HeaderPose(actor,actionTime);
            AnticipateKeeperClaimPose(actor,context.keeperClaim,dt,reset);
            if(MatchSimulation.HasRecordedKeeperClaim(actor))RecordedKeeperClaimPose(actor,actionTime);
            else if(actor.action=="claim"){
                float gather=Mathf.SmoothStep(0,1,(.8f-actionTime)/.45f);body.localPosition+=Vector3.down*(.12f*(1-gather));Rotate("spine02",Mathf.Lerp(28,5,gather),0,0);
                var local=transform.InverseTransformPoint(ballPosition);var start=new Vector3(Mathf.Clamp(local.x,-.35f,.35f),Mathf.Clamp(local.y,.28f,1.65f),.48f);
                var catchPoint=transform.TransformPoint(Vector3.Lerp(start,new Vector3(0,1.10f,.30f),gather));gripOffset=body.up*.075f;
                SolveArm("L",catchPoint+transform.right*.10f-gripOffset);SolveArm("R",catchPoint-transform.right*.10f-gripOffset);
                OrientKeeperHand(0,catchPoint);OrientKeeperHand(1,catchPoint);
                for(int i=0;i<2;i++){SolveLeg(Sides[i],transform.TransformPoint(new Vector3(i==0?.16f:-.16f,.08f,0)));Limb(Sides[i]).foot.rotation=transform.rotation;}
            }
            if(actor.action=="throw")ThrowPose(actor,actionTime,ballPosition);
            if(actor.action=="dive")KeeperDivePose(actor,actionTime);
            KeeperHandlingPose(actor,actionTime,ballPosition);
            KeeperDistributionPose(actor,actionTime,ballPosition);
            if(initialized&&claimPreviewWeight<=.001f&&!MatchSimulation.HandDistribution(actor.action)&&actor.action!="header"&&actor.action!="claim"&&actor.action!="dive"&&actor.action!="throw"&&actor.action!="keeper-hold"&&actor.action!="place-ball"&&actor.action!="keeper-rise")for(int i=0;i<skeleton.Length;i++){var name=source.bones[i].name;if(name=="head"||name.StartsWith("spine")||name.StartsWith("upperarm")||name.StartsWith("lowerarm"))skeleton[i].localRotation=Quaternion.Slerp(previousPose[i],skeleton[i].localRotation,1-Mathf.Exp(-dt*(actor.action=="kick"?24:14)));}
            // Blend the outgoing body pose into the next action without changing
            // simulation positions or the ball. Kick blending ends before the
            // authoritative contact at 0.18 s, preserving the release-point IK.
            if(transitionRemaining>0){
                transitionRemaining=Mathf.Max(0,transitionRemaining-Mathf.Max(0,dt));
                // A reception can be observed at its exact entry even after a
                // long render interval. Blend from physical action progress,
                // so elapsed zero cannot consume the outgoing pose early.
                if(MatchSimulation.IsBodyControl(actor)){
                    float duration=MatchSimulation.BodyControlDuration(actor.actionKind);
                    float elapsedBody=Mathf.Clamp(duration-actionTime,0,duration);
                    transitionRemaining=Mathf.Max(transitionRemaining,Mathf.Max(0,transitionDuration-elapsedBody));
                    if(PreparedReceptionHandoff(actor,elapsedBody))transitionRemaining=transitionDuration;
                }
                if(actor.action=="kick")transitionRemaining=Mathf.Min(transitionRemaining,Mathf.Max(0,.15f-(.64f-actionTime)));
                if(actor.action=="header"){
                    float elapsedHeader=Mathf.Clamp(.64f-actionTime,0,.64f);
                    transitionRemaining=Mathf.Max(transitionRemaining,Mathf.Max(0,transitionDuration-elapsedHeader));
                    transitionRemaining=Mathf.Min(transitionRemaining,Mathf.Max(0,(actor.actionContactTime>0?actor.actionContactTime:.12f)-.02f-elapsedHeader));
                }
                if(MatchSimulation.HasRecordedKeeperClaim(actor))transitionRemaining=Mathf.Min(transitionRemaining,Mathf.Max(0,actor.actionContactTime-.005f-(MatchSimulation.KeeperClaimPresentationDuration-actionTime)));
                if(actor.action=="dive")transitionRemaining=Mathf.Min(transitionRemaining,Mathf.Max(0,(actor.actionContactTime>0?actor.actionContactTime:.2f)-.02f-(1.2f-actionTime)));
                if(actor.action=="tackle"&&actor.actionKind==MatchSimulation.StandingDuel)transitionRemaining=Mathf.Min(transitionRemaining,Mathf.Max(0,MatchSimulation.TacklePreparation-.02f-(MatchSimulation.TacklePreparation+MatchSimulation.TackleRecovery-actionTime)));
                if(MatchSimulation.HandDistribution(actor.action))transitionRemaining=Mathf.Min(transitionRemaining,Mathf.Max(0,actor.actionContactTime-.02f-(MatchSimulation.KeeperDistributionDuration-actionTime)));
                float blend=Mathf.SmoothStep(0,1,1-transitionRemaining/transitionDuration);
                body.localPosition=Vector3.Lerp(transitionBodyPosition,body.localPosition,blend);
                body.localRotation=Quaternion.Slerp(transitionBodyRotation,body.localRotation,blend);
                for(int i=0;i<skeleton.Length;i++)skeleton[i].localRotation=Quaternion.Slerp(transitionPose[i],skeleton[i].localRotation,blend);
                if(actor.action!="dive"&&actor.action!="header")foreach(var side in Sides){var foot=Limb(side).foot;if(foot.position.y<.055f){var floor=foot.position;floor.y=.08f;SolveLeg(side,floor);}}
            }
            BodyReceptionPose(actor,actionTime);
            GroundContactPose(actor,actionTime,ballPosition);
            AerialContestPose(actor,actionTime);
            if(actor.action=="control"&&!MatchSimulation.IsBodyControl(actor)){
                var localBall=transform.InverseTransformPoint(ballPosition);
                if(localBall.y<.65f&&new Vector2(localBall.x,localBall.z).magnitude<.85f){
                    float elapsed=Mathf.Clamp(.3f-actionTime,0,.3f);
                    float touch=Mathf.SmoothStep(0,1,elapsed/.08f)*(1-Mathf.SmoothStep(0,1,(elapsed-.19f)/.11f));
                    var side=receivingLeft?"L":"R";var foot=Limb(side).foot;var contact=BootTarget(ballPosition);
                    SolveLeg(side,Vector3.Lerp(foot.position,contact,touch));foot.rotation=transform.rotation;
                }
            }
            PivotFootwork(actor,dt,reset);
            StopStepFootwork(actor,dt,reset,context);
            BodyReceptionPreparation(actor,context);
            FinalBallContacts(actor,actionTime,alpha,ballPosition,context);
            FinalDuelContact(actor,actionTime);
            KeepBootsAbovePitch(actor.action=="dive"?body.forward:transform.forward,actor.action=="slide"&&slideHasEntryPose&&MatchSimulation.SlidingDuelDuration-actionTime<.15f);
            HandPose(actor,actionTime,dt,reset,context);
            FinalKeeperDistributionContact(actor,actionTime,ballPosition);
            lastPosition=transform.position;lastRotation=transform.rotation;initialized=true;
        }
        void SolveLeg(string side,Vector3 target,Vector3 bendPole=default)
        {
            var hip=Limb(side).upperLeg;var knee=Limb(side).lowerLeg;var foot=Limb(side).foot;
            float upper=Vector3.Distance(hip.position,knee.position),lower=Vector3.Distance(knee.position,foot.position);var delta=target-hip.position;float distance=Mathf.Clamp(delta.magnitude,.10f,upper+lower-.005f);var direction=delta.normalized;
            var bend=Vector3.ProjectOnPlane(bendPole.sqrMagnitude>.001f?bendPole:transform.forward,direction).normalized;
            if(bend.sqrMagnitude<.001f)bend=Vector3.ProjectOnPlane(transform.up,direction).normalized;
            float along=(upper*upper+distance*distance-lower*lower)/(2*distance);float lift=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));var desiredKnee=hip.position+direction*along+bend*lift;
            hip.rotation=Quaternion.FromToRotation(knee.position-hip.position,desiredKnee-hip.position)*hip.rotation;
            knee.rotation=Quaternion.FromToRotation(foot.position-knee.position,target-knee.position)*knee.rotation;
        }
        void SolveArm(string side,Vector3 target,Vector3 elbowPole=default)
        {
            var shoulder=Limb(side).upperArm;var elbow=Limb(side).lowerArm;var hand=Limb(side).wrist;float upper=Vector3.Distance(shoulder.position,elbow.position),lower=Vector3.Distance(elbow.position,hand.position);var delta=target-shoulder.position;float distance=Mathf.Clamp(delta.magnitude,.05f,upper+lower-.005f);var direction=delta.normalized;var bend=Vector3.ProjectOnPlane(elbowPole.sqrMagnitude>.001f?elbowPole:body.forward,direction).normalized;float along=(upper*upper+distance*distance-lower*lower)/(2*distance);var desired=shoulder.position+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));shoulder.rotation=Quaternion.FromToRotation(elbow.position-shoulder.position,desired-shoulder.position)*shoulder.rotation;elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,target-elbow.position)*elbow.rotation;
        }
        void Rotate(string name,float x,float y,float z){if(bones.TryGetValue(name,out var b))b.localRotation=Quaternion.Euler(x,y,z);}
        public static Material Material(Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",.16f);return m;}
        static void Dispose(UnityEngine.Object item){if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        void OnDestroy(){DisposeMecanim();ReleasePrint();if(ownedMaterials!=null)foreach(var material in ownedMaterials)Dispose(material);if(glovePalm!=null)Dispose(glovePalm);if(gloveBack!=null)Dispose(gloveBack);}
    }
}


