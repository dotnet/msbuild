param (
    $repoRoot = $null,
    $verbosity = 'minimal'
    )

. $PSScriptRoot\restore-toolset.ps1 -skipVcpkg

try {
  Set-Location $repoRoot

  $coverageResultsDir = Join-Path $repoRoot "artifacts\CoverageResults"
  $testResultsDir = Join-Path $repoRoot "artifacts\TestResults"
  Remove-Item -Force -Recurse $coverageResultsDir -ErrorAction SilentlyContinue

  $dotnetCoverageTool = Join-Path $repoRoot ".tools\dotnet-coverage\dotnet-coverage.exe"
  
  $mergedCoverage = Join-Path $coverageResultsDir "merged.coverage"
  $mergedCobertura = Join-Path $coverageResultsDir "merged.cobertura.xml"

  if (!(Test-Path $coverageResultsDir -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $coverageResultsDir
  }

  & "$dotnetCoverageTool" merge -o $mergedCoverage $testResultsDir\**\*.coverage
  & "$dotnetCoverageTool" merge -o $mergedCobertura -f cobertura $mergedCoverage
}
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'Coverage' -Message $_
  ExitWithExitCode 1
}