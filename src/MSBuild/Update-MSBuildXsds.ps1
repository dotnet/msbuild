$ErrorActionPreference = 'Stop'

$vsXsdPath = (Join-Path $env:RepoRoot "src\xmake\XMakeCommandLine")

Copy-Item -Path (Join-Path $PSScriptRoot "Microsoft.Build.xsd") -Destination $vsXsdPath -Force
Copy-Item -Path (Join-Path $PSScriptRoot "MSBuild\Microsoft.Build.CommonTypes.xsd") -Destination $vsXsdPath -Force
Copy-Item -Path (Join-Path $PSScriptRoot "MSBuild\Microsoft.Build.Core.xsd") -Destination $vsXsdPath -Force
