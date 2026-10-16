param([string]$Ymm4Directory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'common.ps1')
$Ymm4Directory=Resolve-Ymm4Directory -Candidate $Ymm4Directory -ProjectRoot $projectRoot
Push-Location -LiteralPath $projectRoot
try {
 & dotnet build src/AIVoice2Bridge.YMM4/AIVoice2Bridge.YMM4.csproj -c Release --nologo "-p:Ymm4Directory=$Ymm4Directory"
 if($LASTEXITCODE){throw 'YMM4 plugin build failed'}
 $package=Join-Path $projectRoot 'dist\package'
 New-Item -ItemType Directory -Force -Path $package | Out-Null
 $dll=Join-Path $projectRoot 'src\AIVoice2Bridge.YMM4\bin\Release\net10.0-windows10.0.19041.0\AIVoice2Bridge.YMM4.dll'
 Copy-Item -LiteralPath $dll -Destination (Join-Path $package 'AIVoice2Bridge.YMM4.dll') -Force
 Copy-Item -LiteralPath docs/INSTALLATION.md -Destination (Join-Path $package 'README.md') -Force
 $hash=(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()
 Set-Content -LiteralPath (Join-Path $package 'SHA256.txt') -Value ($hash+'  AIVoice2Bridge.YMM4.dll') -Encoding ascii
 $revision=(& git rev-parse HEAD).Trim()
 if($LASTEXITCODE){throw 'Source revision is unavailable'}
 @{version='0.1.0';protocol=2;ymm4_version='4.56.1.1';editor_version='2.13.0';source_commit=$revision;dll_sha256=$hash}|ConvertTo-Json|
  Set-Content -LiteralPath (Join-Path $package 'release.json') -Encoding utf8
 $zip=Join-Path $projectRoot 'dist\AIVoice2Bridge.YMM4-0.1.0.zip'
 Compress-Archive -LiteralPath (Join-Path $package 'AIVoice2Bridge.YMM4.dll'),(Join-Path $package 'README.md'),(Join-Path $package 'SHA256.txt'),(Join-Path $package 'release.json') -DestinationPath $zip -Force
 $ymme=Join-Path $projectRoot 'dist\AIVoice2Bridge.YMM4-0.1.0.ymme'
 Copy-Item -LiteralPath $zip -Destination $ymme -Force
 Copy-Item -LiteralPath $dll -Destination (Join-Path $projectRoot 'dist\AIVoice2Bridge.YMM4.dll') -Force
 @($zip,$ymme,(Join-Path $projectRoot 'dist\AIVoice2Bridge.YMM4.dll'))|ForEach-Object {((Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant())+'  '+[IO.Path]::GetFileName($_)}|
  Set-Content -LiteralPath (Join-Path $projectRoot 'dist\SHA256SUMS.txt') -Encoding ascii
 Write-Output "Plugin DLL SHA256: $hash"
} finally {Pop-Location}
