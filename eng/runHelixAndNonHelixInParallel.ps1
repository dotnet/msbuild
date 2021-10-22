    [CmdletBinding(PositionalBinding = $false)]
    Param(
        [string] $configuration,
        [string] $buildSourcesDirectory,
        [string] $customHelixTargetQueue,
        [string] $officialBuildIdArgs,
        [switch] $test,
        [switch] $windows
    )

    if (-not $test)
    {
        Write-Output "No '-test' switch. Skip both helix and non helix tests"
        return
    }

    workflow runHelixAndNonHelixInParallel
    {
        Param(
            [string] $configuration,
            [string] $buildSourcesDirectory,
            [string] $customHelixTargetQueue,
            [string] $engfolderPath,
            [string] $officialBuildIdArgs,
            [boolean] $windows
        )

        $runTestsCannotRunOnHelixArgs = ("-configuration", $configuration, "-ci")
        if (-Not $windows)
        {
            InlineScript{$runTestsCannotRunOnHelixArgs.Add($officialBuildIdArgs)}
        }
        $runTestsOnHelixArgs = ("-configuration", $configuration,
        "-prepareMachine",
        "-ci",
        "-restore",
        "-test",
        "-projects", "$buildSourcesDirectory/src/Tests/UnitTests.proj",
        "/bl:$buildSourcesDirectory/artifacts/log/$configuration/TestInHelix.binlog",
        "/p:_CustomHelixTargetQueue=$customHelixTargetQueue")

        if (-Not $windows)
        {
            Invoke-Expression "&'$engfolderPath/runTestsCannotRunOnHelix.sh' $runTestsCannotRunOnHelixArgs"
            Invoke-Expression "&'$engfolderPath/common/build.sh' $runTestsOnHelixArgs"
        }
        else
        {
            parallel {
                Invoke-Expression "&'$engfolderPath\runTestsCannotRunOnHelix.ps1' $runTestsCannotRunOnHelixArgs"
                Invoke-Expression "&'$engfolderPath\common\build.ps1' $runTestsOnHelixArgs"
            }
        }
    }

    runHelixAndNonHelixInParallel -configuration $configuration -buildSourcesDirectory $buildSourcesDirectory -customHelixTargetQueue $customHelixTargetQueue -engfolderPath $PSScriptRoot -windows:$windows -officialBuildIdArgs $officialBuildIdArgs
