    [CmdletBinding(PositionalBinding = $false)]
    Param(
        [string] $configuration,
        [string] $buildSourcesDirectory,
        [string] $customHelixTargetQueue,
        [switch] $test
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
            [string] $engfolderPath
        )

        $runTestsCannotRunOnHelixArgs = ("-configuration", $configuration, "-ci")

        parallel {
            Invoke-Expression "&'$engfolderPath\runTestsCannotRunOnHelix.ps1' $runTestsCannotRunOnHelixArgs"
            Invoke-Expression "&'$engfolderPath\runHelixTests.ps1' -configuration $configuration -buildSourcesDirectory '$buildSourcesDirectory' -customHelixTargetQueue $customHelixTargetQueue -engfolderPath '$engfolderPath'"
        }
    }

    runHelixAndNonHelixInParallel -configuration $configuration -buildSourcesDirectory $buildSourcesDirectory -customHelixTargetQueue $customHelixTargetQueue -engfolderPath $PSScriptRoot