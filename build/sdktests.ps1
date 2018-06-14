[CmdletBinding(PositionalBinding=$false)]
Param(
    [switch] $install,
    [switch] $uninstall,
    [switch] $run,
    [string] $packageVersion = "",
    [string] $tests = "",
    [Parameter(ValueFromRemainingArguments=$true)][String[]]$additionalRunParams
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$lkgPackageVersion = "2.1.400-preview-63001-03"

if ($packageVersion -eq "")
{
    $packageVersion = $lkgPackageVersion
}

$testNames = @()

if ($tests -eq "")
{
    $testNames = "Build", "Clean", "Pack", "Perf", "Publish", "Rebuild", "Restore", "ToolPack"
}
else
{
    $testNames = $tests.split(",")
}

if ($uninstall)
{
    foreach ( $name in $testNames )
    {
        dotnet tool uninstall -g "testSdk$name"
    }
}

if ($install)
{
    foreach ( $name in $testNames )
    {
        dotnet tool install -g "testSdk$name" --version $packageVersion --add-source https://dotnet.myget.org/F/dotnet-cli/api/v3/index.json
    }
}

if ($run)
{
    $failedTests = @()

    foreach ( $name in $testNames )
    {
        $cmd = "testSdk$name"
        
        & $cmd -xml ($name + "results.xml") $additionalRunParams

        if ($LASTEXITCODE -ne 0)
        {
            $failedTests += $name
        }
    }

    if (@($failedTests).Count -gt 0)
    {
        Write-Error "Tests failed: $failedTests"
        Exit 1
    }
    else
    {
        Write-Output "Tests passed"
    }
}
