param (
    $repoRoot = $null,
    $verbosity = 'minimal',
    [Switch]$deleteCoverageReportDir
)

. $PSScriptRoot\restore-toolset.ps1 -skipVcpkg

try {
  Set-Location $repoRoot

  $coverageResultsDir = Join-Path $repoRoot "artifacts\CoverageResults"
  $testResultsDir = Join-Path $repoRoot "artifacts\TestResults"
  Remove-Item -Force -Recurse $coverageResultsDir -ErrorAction SilentlyContinue

  $dotnetCoverageTool = Join-Path $repoRoot ".tools\dotnet-coverage\dotnet-coverage.exe"
  $reportGeneratorTool = Join-Path $repoRoot ".tools\reportgenerator\reportgenerator.exe"
  
  $mergedCoverage = Join-Path $coverageResultsDir "merged.coverage"
  $mergedCobertura = Join-Path $coverageResultsDir "merged.cobertura.xml"
  $coverageReportZip = Join-Path $coverageResultsDir "coverage-report.zip"
  $coverageReportDir = Join-Path $repoRoot "artifacts\CoverageResultsHtml"

  if (!(Test-Path $coverageResultsDir -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $coverageResultsDir
  }

  & "$dotnetCoverageTool" merge -o $mergedCoverage $testResultsDir\**\*.coverage
  & "$dotnetCoverageTool" merge -o $mergedCobertura -f cobertura $mergedCoverage
  & "$reportGeneratorTool" -reports:$mergedCobertura -targetDir:$coverageReportDir -reporttypes:HtmlInline_AzurePipelines
  Compress-Archive -Path $coverageReportDir\* -DestinationPath $coverageReportZip

  if ($deleteCoverageReportDir)
  {
    Remove-Item -Force -Recurse $coverageReportDir -ErrorAction SilentlyContinue
  }
}
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'Coverage' -Message $_
  ExitWithExitCode 1
}