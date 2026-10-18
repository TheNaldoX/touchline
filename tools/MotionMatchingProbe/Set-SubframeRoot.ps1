param([string]$Checkout,[bool]$Enabled)
$ErrorActionPreference='Stop'
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
$current=[IO.File]::ReadAllText($path).Replace("`r`n","`n").TrimEnd()
if($current -cne $original.TrimEnd() -and $current -cne $patched.TrimEnd()){throw 'Unexpected external controller edits; refusing overwrite'}
if($patched -ceq $original){throw 'Adapter did not apply'}
[IO.File]::WriteAllText($path, $(if($Enabled){$patched}else{$original})+"`n")
Copy-Item -LiteralPath "$PSScriptRoot/SubframeRoot.cs" -Destination (Join-Path $Checkout 'com.jlpm.motionmatching/Runtime/Core/SubframeRoot.cs')
