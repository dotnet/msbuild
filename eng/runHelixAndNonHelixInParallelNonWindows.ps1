  [CmdletBinding(PositionalBinding = $false)]
  Param(
      [string] $configuration,
      [string] $buildSourcesDirectory,
      [string] $customHelixTargetQueue,
      [string] $officialBuildIdArgs,
      [string] $additionalMSBuildParameters = "",
      [switch] $test
  )

  if (-not $test)
  {
      Write-Output "No '-test' switch. Skip both helix and non helix tests"
      return
  }

  $runTestsCannotRunOnHelixArgs = ("-configuration", $configuration, "-ci", $officialBuildIdArgs) + $additionalMSBuildParameters.Split(' ')
  $runTestsOnHelixArgs = ("-configuration", $configuration,
    "-ci",
  "-restore",
  "-test",
  "-projects", "$buildSourcesDirectory/src/Tests/UnitTests.proj",
  "/bl:$buildSourcesDirectory/artifacts/log/$configuration/TestInHelix.binlog",
  "/p:_CustomHelixTargetQueue=$customHelixTargetQueue") + $additionalMSBuildParameters.Split(' ')

  $runTests = ("&'$PSScriptRoot/runTestsCannotRunOnHelix.sh' $runTestsCannotRunOnHelixArgs", "&'$PSScriptRoot/common/build.sh' $runTestsOnHelixArgs")

  $runTests | ForEach-Object -Parallel { Invoke-Expression $_}

  # An array of names of processes to stop on script exit
  $processesToStopOnExit =  @('msbuild', 'dotnet', 'vbcscompiler')

  Write-Host 'Stopping running build processes...'
  foreach ($processName in $processesToStopOnExit) {
    Get-Process -Name $processName -ErrorAction SilentlyContinue | Stop-Process
  }
