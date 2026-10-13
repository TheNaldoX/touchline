param([Parameter(Mandatory=$true)][string]$Archive)
$ErrorActionPreference='Stop'
if([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT -and @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'").Count){throw 'Close Unity before installing the personal mocap files.'}
$expected='1A26159823911A0B509CEE8B82E1F9916714DCF0DC71E90139ECE7FC99792C44'
if((Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash -ne $expected){throw 'This is not the verified Soccer pack 05 archive.'}
$root=Split-Path $PSScriptRoot -Parent
$destination=Join-Path $root 'unity/Touchline/Assets/Touchline/Editor/PrivateSoccerMocap'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Archive).Path)
try{
    $entries=@($zip.Entries|Where-Object {$_.FullName -match '^mocap/Rokoko_Default/[0-9]{2}_[A-Za-z0-9_]+\.fbx$'})
    if($entries.Count -ne 21){throw 'Expected 21 Rokoko_Default animations.'}
    [IO.Directory]::CreateDirectory($destination)|Out-Null
    foreach($entry in $entries){
        $target=Join-Path $destination ([IO.Path]::GetFileName($entry.FullName))
        if(Test-Path -LiteralPath $target){
            # Reuse an identical import, but never silently overwrite a local modification.
            $sha=[Security.Cryptography.SHA256]::Create();$stream=$entry.Open()
            try{$hash=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
            if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash){throw "Local asset differs: $target"}
        }else{[IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$false)}
    }
}finally{$zip.Dispose()}
Write-Output '21 FBXs ready. Run Unity menu Touchline/Animations/Import personal soccer mocap, or batch executeMethod Touchline.Editor.SoccerMocapImport.Batch.'
# The raw pack and exported curves are intentionally ignored by Git. License:
# https://andersonrohr.gumroad.com/l/mocap_pack5 (personal/commercial game use;
# no redistribution, resale or repackaging as a standalone product).
