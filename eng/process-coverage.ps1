param (
    $repoRoot = $null,
    $coverageArtifactsDir = $null,
    $coverageReportName = 'merged',
    $verbosity = 'minimal'
    )

. $PSScriptRoot\restore-dotnet-coverage.ps1

try {
  Set-Location $repoRoot

  $coverageResultsDir = $coverageArtifactsDir
  $testResultsDir = Join-Path $repoRoot "artifacts\TestResults"
  Remove-Item -Force -Recurse $coverageResultsDir -ErrorAction SilentlyContinue

  $dotnetCoverageTool = Join-Path $repoRoot ".tools\dotnet-coverage\dotnet-coverage.exe"

  $mergedCoverage = Join-Path $coverageResultsDir $coverageReportName".coverage"
  $mergedCobertura = Join-Path $coverageResultsDir $coverageReportName".cobertura.xml"

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