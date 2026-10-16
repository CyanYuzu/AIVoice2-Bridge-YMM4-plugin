param([string]$Ymm4Directory,[switch]$Uninstall)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'common.ps1')
$Ymm4Directory=Resolve-Ymm4Directory -Candidate $Ymm4Directory -ProjectRoot $projectRoot
$ymmRoot=(Resolve-Path -LiteralPath $Ymm4Directory).Path
if(!(Test-Path -LiteralPath (Join-Path $ymmRoot 'YukkuriMovieMaker.exe'))){throw 'YMM4 installation is missing'}
$targetDirectory=Join-Path $ymmRoot 'user\plugin\AIVoice2Bridge'
$targetDll=Join-Path $targetDirectory 'AIVoice2Bridge.YMM4.dll'
if($Uninstall){
 if(Test-Path -LiteralPath $targetDll){Remove-Item -LiteralPath $targetDll -ErrorAction Stop}
 Write-Output 'Plugin DLL removed. Restart YMM4 to unload it.'
 exit 0
}
$source=Join-Path $projectRoot 'dist\package\AIVoice2Bridge.YMM4.dll'
if(!(Test-Path -LiteralPath $source)){throw 'Run scripts/build.ps1 before installation'}
$expected=(Get-Content -LiteralPath (Join-Path $projectRoot 'dist\package\SHA256.txt') -Raw).Trim().Split(' ')[0]
if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected){throw 'Package DLL hash mismatch'}
New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
if(Test-Path -LiteralPath $targetDll){
 if((Get-FileHash -LiteralPath $targetDll -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected){
  Copy-Item -LiteralPath $targetDll -Destination ($targetDll+'.backup-'+(Get-Date -Format yyyyMMdd-HHmmss))
 }
}
Copy-Item -LiteralPath $source -Destination $targetDll -Force
if((Get-FileHash -LiteralPath $targetDll -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected){throw 'Installed plugin hash mismatch'}
@{at=(Get-Date).ToString('o');target=$targetDll;sha256=$expected;restart_required=$true} | ConvertTo-Json |
 Set-Content -LiteralPath (Join-Path $projectRoot 'evidence\installation.json') -Encoding utf8
Write-Output "Installed $targetDll. Restart YMM4 and refresh voices in A.I.VOICE2 Bridge settings."
