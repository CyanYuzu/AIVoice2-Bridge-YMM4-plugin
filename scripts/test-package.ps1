$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$dist=Join-Path $projectRoot 'dist'
$zip=Join-Path $dist 'AIVoice2Bridge.YMM4-0.1.0.zip'
$ymme=Join-Path $dist 'AIVoice2Bridge.YMM4-0.1.0.ymme'
if((Get-FileHash -LiteralPath $zip).Hash -ne (Get-FileHash -LiteralPath $ymme).Hash){throw 'YMME differs from ZIP'}
$run=Join-Path $projectRoot ('build\release-validation\'+[guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $zip -DestinationPath $run
$expectedNames=@('AIVoice2Bridge.YMM4.dll','README.md','SHA256.txt','release.json')
$actualNames=@(Get-ChildItem -LiteralPath $run -File|Select-Object -ExpandProperty Name)
if(Compare-Object ($expectedNames|Sort-Object) ($actualNames|Sort-Object)){throw 'Unexpected plugin package contents'}
$dll=Join-Path $run 'AIVoice2Bridge.YMM4.dll'
$expected=(Get-Content -LiteralPath (Join-Path $run 'SHA256.txt') -Raw).Trim().Split(' ')[0]
if((Get-FileHash -LiteralPath $dll).Hash.ToLowerInvariant() -ne $expected){throw 'Extracted DLL hash mismatch'}
$accepted=Join-Path $projectRoot 'build\acceptance\AIVoice2Bridge.YMM4.dll'
if(!(Test-Path -LiteralPath $accepted) -or (Get-FileHash -LiteralPath $accepted).Hash.ToLowerInvariant() -ne $expected){throw 'Acceptance host did not test this exact payload'}
$summary=@{zip_equals_ymme=$true;archive_entries=$actualNames;dll_sha256=$expected;acceptance_payload_matches=$true;installer_gui_exercised=$false}
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'evidence') -Force|Out-Null
$summary|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $projectRoot 'evidence\release-package.json') -Encoding utf8
Write-Output 'PASS package contents, YMME format and exact tested DLL'
