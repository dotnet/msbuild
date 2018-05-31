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

$lkgPackageVersion = "1.0.0-preview-62918-03"

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
    foreach ( $name in $testNames )
    {
        $cmd = "testSdk$name"
        
        & $cmd -xml ($name + "results.xml") $additionalRunParams
    }
}
