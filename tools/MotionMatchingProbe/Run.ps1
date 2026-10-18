param([ValidateSet(1,22)][int]$Actors=1,[ValidateSet(30,60,120)][int]$Hz=60,[string]$Name="mm-$Actors-$Hz",[switch]$SubframeRoot,[switch]$SubframePose,[switch]$FootOrientation)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
$checkout=Join-Path $root '.validation/external-research/MotionMatching'
$project=Join-Path $checkout 'minimalproject'
$commit='35fc8ee9a0fd97f12de458ea81eaef7c8219d87a'
if((git -C $checkout rev-parse HEAD).Trim() -ne $commit){throw 'Unexpected upstream revision'}
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Unity is already running'}
if($FootOrientation){$SubframePose=[switch]$true}
& "$PSScriptRoot/Set-SubframeRoot.ps1" -Checkout $checkout -Enabled $SubframeRoot.IsPresent -FullPose $SubframePose.IsPresent -FootOrientation $FootOrientation.IsPresent
if($Name -notmatch '^[a-zA-Z0-9-]+$'){throw 'Invalid proof name'}
$output=Join-Path $root ".validation/$Name"
if(Test-Path -LiteralPath $output){throw 'Proof already exists; choose a new name'}
New-Item -ItemType Directory $output | Out-Null
@{upstream=$commit;subframeRoot=($SubframeRoot.IsPresent -or $SubframePose.IsPresent);subframePose=$SubframePose.IsPresent;footOrientation=$FootOrientation.IsPresent;actors=$Actors;hz=$Hz;warmupSeconds=2;measuredSeconds=20} | ConvertTo-Json | Set-Content "$output/source.json"
$samples=Join-Path $project 'Assets/Examples'
if(!(Test-Path -LiteralPath $samples)){
    # Import upstream samples into its own disposable project, never into Touchline.
    Copy-Item -LiteralPath "$checkout/com.jlpm.motionmatching/Samples~/Examples" -Destination $samples -Recurse
}
$editor=Join-Path $project 'Assets/Editor'
New-Item -ItemType Directory -Force $editor | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/Probe.cs" -Destination "$editor/Probe.cs"
Copy-Item -LiteralPath "$PSScriptRoot/PoseMetrics.cs" -Destination "$editor/PoseMetrics.cs"
Copy-Item -LiteralPath "$PSScriptRoot/FootContactChecks.cs" -Destination "$editor/FootContactChecks.cs"
$profile=Join-Path $checkout 'com.jlpm.motionmatching/Runtime/Utils/PROFILE.cs'
$source=[IO.File]::ReadAllText($profile).Replace('//#define PROFILE_MOTION_MATCHING','#define PROFILE_MOTION_MATCHING')
if(!$source.Contains('public double TotalMs;')){
    $source=$source.Replace('public float MinTicks, MinMs;',"public double TotalMs; public int Count;`n            public float MinTicks, MinMs;")
    $source=$source.Replace('MinTicks = Mathf.Min(MinTicks, sampleTicks);','TotalMs += sampleMs; Count++; MinTicks = Mathf.Min(MinTicks, sampleTicks);')
}
[IO.File]::WriteAllText($profile,$source)
$unity='C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe'
$args=@('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod','TouchlineMotionMatchingProbe.Run','-probeActors',"$Actors",'-probeHz',"$Hz",'-probeOutput',('"'+$output+'"'),'-logFile',('"'+$output+'/unity.log"'))
$p=Start-Process -FilePath $unity -ArgumentList $args -WindowStyle Hidden -PassThru
$p.WaitForExit()
if($p.ExitCode -ne 0){throw "Unity failed: $($p.ExitCode). See $output/unity.log"}
if(!(Test-Path "$output/result.json")){throw 'No benchmark result'}
Get-Content "$output/result.json"
