function Resolve-Ymm4Directory {
 param([string]$Candidate,[string]$ProjectRoot)
 if([string]::IsNullOrWhiteSpace($Candidate)){$Candidate=$env:YMM4_PATH}
 if([string]::IsNullOrWhiteSpace($Candidate)){
  $localProperties=Join-Path $ProjectRoot 'Directory.Build.props'
  if(Test-Path -LiteralPath $localProperties){
   [xml]$configuration=Get-Content -LiteralPath $localProperties -Raw
   $node=$configuration.SelectSingleNode('/Project/PropertyGroup/Ymm4Directory')
   if($node){$Candidate=$node.InnerText}
  }
 }
 if([string]::IsNullOrWhiteSpace($Candidate)){
  throw 'Specify -Ymm4Directory, set YMM4_PATH, or create Directory.Build.props from the example.'
 }
 $resolved=(Resolve-Path -LiteralPath $Candidate).Path
 if(!(Test-Path -LiteralPath (Join-Path $resolved 'YukkuriMovieMaker.exe'))){throw 'The selected directory does not contain YukkuriMovieMaker.exe'}
 return $resolved
}
