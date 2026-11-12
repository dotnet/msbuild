[CmdletBinding(PositionalBinding=$false)]
param (
  [string]$configuration,
  [string]$vsDropName,
  [string]$bootstrapperInfo
)

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "build-utils.ps1")

function Restore-Repo() {
  $toolsetBuildProj = InitializeToolset

  MSBuild $toolsetBuildProj `
    /p:RepoRoot=$RepoRoot `
    /p:Restore=$true `
}

try {
  Restore-Repo

  $optProfToolDir = Get-PackageDir "Roslyn.OptProf.RunSettings.Generator"

  $optProfToolExe = Join-Path $optProfToolDir "tools\roslyn.optprof.runsettings.generator.exe"

  $configFile = Join-Path $EngRoot "config\OptProf.json"
  $runSettingsFile = Join-Path $VSSetupDir "Insertion\RunSettings\MSBuildOptProf.runsettings"

  Exec-Console $optProfToolExe "--config $configFile --vsDropName $vsDropName --bootstrapperInfo $bootstrapperInfo --out $runSettingsFile"

  exit 0
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  exit 1
}
finally {
  Pop-Location
}
