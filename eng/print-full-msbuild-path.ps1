. $PSScriptRoot\common\tools.ps1
function GetFullMsbuildPath {
    $env:DOTNET_SDK_TEST_MSBUILD_PATH = InitializeVisualStudioMSBuild -install:$false -vsRequirements:$GlobalJson.tools.'vs-opt'
}

GetFullMsbuildPath
$env:DOTNET_SDK_TEST_MSBUILD_PATH
