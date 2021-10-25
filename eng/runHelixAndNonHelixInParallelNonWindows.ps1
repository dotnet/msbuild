    [CmdletBinding(PositionalBinding = $false)]
    Param(
        [string] $configuration,
        [string] $buildSourcesDirectory,
        [string] $customHelixTargetQueue,
        [string] $officialBuildIdArgs,
        [switch] $test
    )

    if (-not $test)
    {
        Write-Output "No '-test' switch. Skip both helix and non helix tests"
        return
    }

    $runTestsCannotRunOnHelixArgs = ("-configuration", $configuration, "-ci", $officialBuildIdArgs.Substring(12))
    $runTestsOnHelixArgs = ("-configuration", $configuration,
    "-prepareMachine",
    "-ci",
    "-restore",
    "-test",
    "-projects", "$buildSourcesDirectory/src/Tests/UnitTests.proj",
    "/bl:$buildSourcesDirectory/artifacts/log/$configuration/TestInHelix.binlog",
    "/p:_CustomHelixTargetQueue=$customHelixTargetQueue")

   $runTests = ("&'$PSScriptRoot/runTestsCannotRunOnHelix.sh' $runTestsCannotRunOnHelixArgs", "&'$PSScriptRoot/common/build.sh' $runTestsOnHelixArgs")

   $runTests | ForEach-Object -Parallel { Invoke-Expression $_}