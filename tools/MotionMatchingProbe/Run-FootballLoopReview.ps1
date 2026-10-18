param([Parameter(Mandatory=$true)][string]$InputMotion,[Parameter(Mandatory=$true)][string]$ReferenceMotion,[Parameter(Mandatory=$true)][string]$Name)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
if($Name -notmatch '^[a-zA-Z0-9-]+$'){throw 'Invalid proof name'}
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Unity active'}
$project=Join-Path $root '.validation/external-research/MotionMatching/minimalproject'
$checkout=Split-Path $project
if((git -C $checkout rev-parse HEAD) -ne '35fc8ee9a0fd97f12de458ea81eaef7c8219d87a'){throw 'Unexpected upstream revision'}
$inputPath=(Resolve-Path -LiteralPath $InputMotion).Path
$reference=(Resolve-Path -LiteralPath $ReferenceMotion).Path
$output=Join-Path $root ".validation/$Name"
New-Item -ItemType Directory $output -ErrorAction Stop|Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/FootballFbxProbe.cs" -Destination "$project/Assets/Editor/FootballFbxProbe.cs"
$args=@('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod','FootballFbxProbe.ReviewLoop','-footballOutput',('"'+$output+'"'),'-footballReference',('"'+$reference+'"'),'-footballInput',('"'+$inputPath+'"'),'-logFile',('"'+$output+'/unity.log"'))
$p=Start-Process 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru
$p.WaitForExit()
if($p.ExitCode -ne 0 -or !(Test-Path "$output/passed.txt")){throw 'Review failed; inspect unity.log'}
Get-Content "$output/passed.txt"
