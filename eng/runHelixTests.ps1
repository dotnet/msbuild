Param(
    [string] $configuration,
    [string] $buildSourcesDirectory,
    [string] $customHelixTargetQueue,
    [string] $engfolderPath
)

Write-Host "Running tests in Helix..."

$runTestsOnHelixArgs = ("-configuration", $configuration,
"-prepareMachine",
"-ci",
"-restore",
"-test",
"-projects", "$buildSourcesDirectory/src/Tests/UnitTests.proj",
"/bl:$buildSourcesDirectory\artifacts\log\$configuration\TestInHelix.binlog",
"/p:_CustomHelixTargetQueue=$customHelixTargetQueue")

$expressionToInvoke = "&'$engfolderPath\common\build.ps1' $runTestsOnHelixArgs"
Write-Host "Invoking $expressionToInvoke"

Invoke-Expression $expressionToInvoke

Write-Host "Done running tests on Helix..."
