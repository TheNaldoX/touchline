param([string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
# Zero simulated seasons: both public summaries must describe the same initialized world.
# EnsureWorld imports the free-agent pool, so the raw database alone is not the oracle.
$output=& $Dotnet run --project (Join-Path $PSScriptRoot 'SeasonSim.csproj') -c Release -- 0 176 --world --report
if($LASTEXITCODE -ne 0){throw 'SeasonSim a échoué.'}
$headerIndex=-1
for($i=0;$i -lt $output.Count;$i++){if($output[$i] -like '*| Joueurs libres |*'){$headerIndex=$i;break}}
if($headerIndex -lt 0 -or $headerIndex+2 -ge $output.Count){throw 'Tableau détaillé des joueurs libres absent.'}
$headers=@(($output[$headerIndex] -split '\|') | ForEach-Object {$_.Trim()})
$values=($output[$headerIndex+2] -split '\|')
$column=[Array]::IndexOf($headers,'Joueurs libres')
$expected=[int]$values[$column]
if($expected -le 0){throw 'Le monde initialisé doit contenir des joueurs libres pour couvrir la régression.'}
$summary=[regex]::Match(($output -join "`n"),'Joueurs sans club : (\d+) \u2192 (\d+)\.')
if(-not $summary.Success){throw 'Résumé des joueurs libres absent.'}
if([int]$summary.Groups[1].Value -ne $expected -or [int]$summary.Groups[2].Value -ne $expected){
    throw "Comptage incohérent : $($summary.Value) Attendu : $expected au début et à la fin."
}
Write-Output "PASS : résumé SeasonSim = $expected joueurs libres au début et à la fin (aucune saison simulée)."
