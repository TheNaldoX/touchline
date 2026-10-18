param([string]$Checkout,[bool]$Enabled,[bool]$FullPose=$false,[bool]$FootOrientation=$false)
$ErrorActionPreference='Stop'
if($FootOrientation){$FullPose=$true}
$relative='com.jlpm.motionmatching/Runtime/Core/MotionMatchingController.cs'
$path=Join-Path $Checkout $relative
$original=(git -C $Checkout show "35fc8ee9a0fd97f12de458ea81eaef7c8219d87a:$relative") -join "`n"
if($LASTEXITCODE -ne 0){throw 'Cannot read pinned controller'}
$patched=$original.Replace('AnimationSpaceOriginPos = mmPose.JointLocalPositions[0];',
    'SubframeRoot.Sample(PoseSet, CurrentFrame, math.frac(CurrentFrameTime), out var rootPosition, out var rootRotation);'+"`n            AnimationSpaceOriginPos = rootPosition;")
$patched=$patched.Replace('InverseAnimationSpaceOriginRot = math.inverse(mmPose.JointLocalRotations[0]);','InverseAnimationSpaceOriginRot = math.inverse(rootRotation);')
$patched=$patched.Replace('float3 localSpacePos = math.mul(InverseAnimationSpaceOriginRot, pose.JointLocalPositions[0] - AnimationSpaceOriginPos);',
    'SubframeRoot.Sample(PoseSet, frameIndex, math.frac(CurrentFrameTime), out var rootPosition, out var rootRotation);'+"`n            float3 localSpacePos = math.mul(InverseAnimationSpaceOriginRot, rootPosition - AnimationSpaceOriginPos);")
$patched=$patched.Replace('quaternion localSpaceRot = math.mul(InverseAnimationSpaceOriginRot, pose.JointLocalRotations[0]);','quaternion localSpaceRot = math.mul(InverseAnimationSpaceOriginRot, rootRotation);')
$posePatched=$patched.Replace('private Inertialization Inertialization;',"private Inertialization Inertialization;`n        private readonly SubframePose sourceSampler = new SubframePose(), targetSampler = new SubframePose();")
$posePatched=$posePatched.Replace('PoseSet.GetPose(frameIndex, out PoseVector pose);','PoseVector pose = sourceSampler.Sample(PoseSet, frameIndex, math.frac(CurrentFrameTime));')
$posePatched=$posePatched.Replace('Inertialization.PoseTransition(PoseSet, CurrentFrame, bestFrame);','Inertialization.PoseTransition(sourceSampler.Sample(PoseSet, CurrentFrame, math.frac(CurrentFrameTime)), targetSampler.Sample(PoseSet, bestFrame, math.frac(CurrentFrameTime)));')
$footPatched=$posePatched.Replace('TwoJointIK.Solve(', 'FootContactIK.Solve(')
$inertialRelative='com.jlpm.motionmatching/Runtime/Inertialization/Inertialization.cs'
$inertialPath=Join-Path $Checkout $inertialRelative
$inertialOriginal=(git -C $Checkout show "35fc8ee9a0fd97f12de458ea81eaef7c8219d87a:$inertialRelative") -join "`n"
if($LASTEXITCODE -ne 0){throw 'Cannot read pinned inertialization'}
$anchor='poseSet.GetPose(targetPoseIndex, out PoseVector targetPose);'
$inertialPatched=$inertialOriginal.Replace($anchor,$anchor+"`n            PoseTransition(sourcePose, targetPose);`n        }`n`n        public void PoseTransition(PoseVector sourcePose, PoseVector targetPose)`n        {")
$inertialCurrent=[IO.File]::ReadAllText($inertialPath).Replace("`r`n","`n").TrimEnd()
if($inertialCurrent -cne $inertialOriginal.TrimEnd() -and $inertialCurrent -cne $inertialPatched.TrimEnd()){throw 'Unexpected inertialization edits'}
$current=[IO.File]::ReadAllText($path).Replace("`r`n","`n").TrimEnd()
if($current -cne $original.TrimEnd() -and $current -cne $patched.TrimEnd() -and $current -cne $posePatched.TrimEnd() -and $current -cne $footPatched.TrimEnd()){throw 'Unexpected external controller edits; refusing overwrite'}
if($patched -ceq $original){throw 'Adapter did not apply'}
[IO.File]::WriteAllText($path, $(if($FootOrientation){$footPatched}elseif($FullPose){$posePatched}elseif($Enabled){$patched}else{$original})+"`n")
[IO.File]::WriteAllText($inertialPath, $(if($FullPose){$inertialPatched}else{$inertialOriginal})+"`n")
Copy-Item -LiteralPath "$PSScriptRoot/SubframeRoot.cs" -Destination (Join-Path $Checkout 'com.jlpm.motionmatching/Runtime/Core/SubframeRoot.cs')
Copy-Item -LiteralPath "$PSScriptRoot/SubframePose.cs" -Destination (Join-Path $Checkout 'com.jlpm.motionmatching/Runtime/Core/SubframePose.cs')
Copy-Item -LiteralPath "$PSScriptRoot/FootContactIK.cs" -Destination (Join-Path $Checkout 'com.jlpm.motionmatching/Runtime/Core/FootContactIK.cs')
