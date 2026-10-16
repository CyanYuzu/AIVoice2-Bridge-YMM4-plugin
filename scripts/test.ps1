param([string]$Ymm4Directory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'common.ps1')
$Ymm4Directory=Resolve-Ymm4Directory -Candidate $Ymm4Directory -ProjectRoot $projectRoot
Push-Location -LiteralPath $projectRoot
try {
 & dotnet build tests/Acceptance/Acceptance.csproj -c Release --nologo "-p:Ymm4Directory=$Ymm4Directory"
 if($LASTEXITCODE){throw 'Developer acceptance host build failed'}
 $hostPath=Join-Path $projectRoot 'build\acceptance\PluginAcceptance.exe'
 & $hostPath $projectRoot $Ymm4Directory
 if($LASTEXITCODE){throw 'Real-editor plugin acceptance failed'}
 & $hostPath $projectRoot $Ymm4Directory --cache-only
 if($LASTEXITCODE){throw 'Cached voice persistence failed'}
} finally {Pop-Location}
