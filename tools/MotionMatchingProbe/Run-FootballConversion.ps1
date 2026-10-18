param([string]$Name='football-conversion')
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
$checkout=Join-Path $root '.validation/external-research/MotionMatching'
$project=Join-Path $checkout 'minimalproject'
if((git -C $checkout rev-parse HEAD) -ne '35fc8ee9a0fd97f12de458ea81eaef7c8219d87a'){throw 'Unexpected upstream revision'}
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Unity already active'}
if($Name -notmatch '^[a-zA-Z0-9-]+$'){throw 'Invalid proof name'}
$output=Join-Path $root ".validation/$Name"
if(Test-Path -LiteralPath $output){throw 'Proof exists'}
New-Item -ItemType Directory $output|Out-Null
$stock=Join-Path $project 'Assets/TouchlineStock'
New-Item -ItemType Directory -Force $stock|Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/FootballFbxProbe.cs" -Destination "$project/Assets/Editor/FootballFbxProbe.cs"
$proof=@()
foreach($clip in @('Soccer Idle','Jog Forward','Standard Run','Two Cycle Sprint')){
    $source=Join-Path $root "unity/Touchline/Assets/Touchline/Resources/Animations/Mixamo/$clip.fbx"
    $dest=Join-Path $stock "$clip.fbx"
    $hash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if(Test-Path -LiteralPath $dest){if((Get-FileHash -LiteralPath $dest).Hash -ne $hash){throw 'Unexpected imported FBX; refusing overwrite'}}else{Copy-Item -LiteralPath $source -Destination $dest}
    $proof+=@{clip=$clip;sha256=$hash;source=$source}
}
$proof|ConvertTo-Json|Set-Content "$output/sources.json"
$args=@('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod','FootballFbxProbe.Run','-footballOutput',('"'+$output+'"'),'-logFile',('"'+$output+'/unity.log"'))
$p=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru
$p.WaitForExit()
if($p.ExitCode -ne 0 -or !(Test-Path "$output/passed.txt")){throw "Conversion failed; see $output/unity.log"}
foreach($item in $proof){if((Get-FileHash -LiteralPath $item.source).Hash -ne $item.sha256){throw 'Source FBX changed'}}
Get-Content "$output/passed.txt"
Get-ChildItem $output -Filter '*.result.json'|ForEach-Object {Get-Content $_.FullName}

