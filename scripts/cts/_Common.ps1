# scripts/cts/_Common.ps1 — shared helpers for Collect-Local.ps1 and Run-Local.ps1
$ErrorActionPreference = 'Stop'

$script:RepoRoot   = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script:DotnetExe  = Join-Path $script:RepoRoot '.dotnet\dotnet.exe'
$script:CtsRoot    = Join-Path $script:RepoRoot '.cts'
$script:BaselineDir= Join-Path $script:CtsRoot 'baseline'
$script:LogsDir    = Join-Path $script:CtsRoot 'logs'
$script:ConfigPath = Join-Path $PSScriptRoot 'cts.config.json'

# (short-key, csproj relative path, output DLL name) for every VSTest variant.
$script:Projects = @(
    [pscustomobject]@{
        Key = 'StringTools'
        CsProj = 'src\StringTools.UnitTests\StringTools.UnitTests.VSTest.csproj'
        Dll = 'Microsoft.NET.StringTools.UnitTests.dll'
        BinDir = 'StringTools.UnitTests.VSTest'
    }
    [pscustomobject]@{
        Key = 'Framework'
        CsProj = 'src\Framework.UnitTests\Microsoft.Build.Framework.UnitTests.VSTest.csproj'
        Dll = 'Microsoft.Build.Framework.UnitTests.dll'
        BinDir = 'Microsoft.Build.Framework.UnitTests.VSTest'
    }
    [pscustomobject]@{
        Key = 'Utilities'
        CsProj = 'src\Utilities.UnitTests\Microsoft.Build.Utilities.UnitTests.VSTest.csproj'
        Dll = 'Microsoft.Build.Utilities.UnitTests.dll'
        BinDir = 'Microsoft.Build.Utilities.UnitTests.VSTest'
    }
    [pscustomobject]@{
        Key = 'EngineOM'
        CsProj = 'src\Build.OM.UnitTests\Microsoft.Build.Engine.OM.UnitTests.VSTest.csproj'
        Dll = 'Microsoft.Build.Engine.OM.UnitTests.dll'
        BinDir = 'Microsoft.Build.Engine.OM.UnitTests.VSTest'
    }
    [pscustomobject]@{
        Key = 'CommandLine'
        CsProj = 'src\MSBuild.UnitTests\Microsoft.Build.CommandLine.UnitTests.VSTest.csproj'
        Dll = 'Microsoft.Build.CommandLine.UnitTests.dll'
        BinDir = 'Microsoft.Build.CommandLine.UnitTests.VSTest'
    }
    [pscustomobject]@{
        Key = 'Engine'
        CsProj = 'src\Build.UnitTests\Microsoft.Build.Engine.UnitTests.VSTest.csproj'
        Dll = 'Microsoft.Build.Engine.UnitTests.dll'
        BinDir = 'Microsoft.Build.Engine.UnitTests.VSTest'
    }
    [pscustomobject]@{
        Key = 'Tasks'
        CsProj = 'src\Tasks.UnitTests\Microsoft.Build.Tasks.UnitTests.VSTest.csproj'
        Dll = 'Microsoft.Build.Tasks.UnitTests.dll'
        BinDir = 'Microsoft.Build.Tasks.UnitTests.VSTest'
    }
    [pscustomobject]@{
        Key = 'BuildCheck'
        CsProj = 'src\BuildCheck.UnitTests\Microsoft.Build.BuildCheck.UnitTests.VSTest.csproj'
        Dll = 'Microsoft.Build.BuildCheck.UnitTests.dll'
        BinDir = 'Microsoft.Build.BuildCheck.UnitTests.VSTest'
    }
)

function Get-Projects {
    param([string]$Filter)
    if (-not $Filter) { return $script:Projects }
    $sel = $script:Projects | Where-Object { $_.Key -ieq $Filter -or $_.CsProj -ieq $Filter }
    if (-not $sel) {
        throw "Unknown -Project '$Filter'. Known keys: $($script:Projects.Key -join ', ')"
    }
    return @($sel)
}

function Ensure-Cli {
    if (-not (Get-Command cts -ErrorAction SilentlyContinue)) {
        throw @"
'cts' is not on PATH. Install with:
  dotnet tool install cts --global --prerelease --add-source https://devdiv.pkgs.visualstudio.com/_packaging/VS/nuget/v3/index.json
"@
    }
    if (-not (Test-Path $script:DotnetExe)) {
        throw "Local dotnet at '$script:DotnetExe' not found. Run .\build.cmd once to bootstrap."
    }
}

function Ensure-CtsConfig {
    if (-not (Test-Path $script:ConfigPath)) {
        $cfg = [ordered]@{
            SourceCodeFiles = [ordered]@{
                Include = @('src/**/*.cs')
                Exclude = @('**/obj/**', '**/bin/**', '**/TestAssets/**')
            }
            Files = [ordered]@{
                Exclude = @('**/*.md', '**/*.yml', '**/*.yaml', '**/*.png', 'documentation/**')
            }
            Modules = [ordered]@{
                Include = @()
                Exclude = @(
                    '**/Microsoft.Testing.*.dll',
                    '**/Microsoft.TestPlatform*.dll',
                    '**/Microsoft.VisualStudio.TestPlatform*.dll',
                    '**/Microsoft.VisualStudio.CodeCoverage*.dll',
                    '**/Microsoft.DotNet.*.dll',
                    '**/xunit.*.dll',
                    '**/Xunit.*.dll',
                    '**/testhost*',
                    '**/Microsoft.Bcl.*.dll',
                    '**/Microsoft.ApplicationInsights.dll',
                    '**/Newtonsoft.Json.dll',
                    '**/Shouldly.dll',
                    '**/AwesomeAssertions.dll',
                    '**/FakeItEasy.dll',
                    '**/Verify.*.dll',
                    '**/System*.dll',
                    '**/runtimes/**'
                )
            }
            Filter = [ordered]@{
                Include = @('**/artifacts/bin/*.VSTest/Debug/net10.0/*.UnitTests.dll')
                Exclude = @('**/*.resources.dll')
            }
        }
        $cfg | ConvertTo-Json -Depth 6 | Set-Content -NoNewline -Path $script:ConfigPath
        Write-Host "Wrote default CTS config to $script:ConfigPath" -ForegroundColor DarkGray
    }
}

function Build-Project {
    param([Parameter(Mandatory)] $Project)
    $csproj = Join-Path $script:RepoRoot $Project.CsProj
    Write-Host "  build $($Project.Key) ..." -ForegroundColor DarkGray -NoNewline
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = & $script:DotnetExe build -c Debug $csproj -f net10.0 -v:q -nologo 2>&1
    $sw.Stop()
    if ($LASTEXITCODE -ne 0) {
        Write-Host " FAIL ($([int]$sw.Elapsed.TotalSeconds)s)" -ForegroundColor Red
        $out | Select-Object -Last 30 | ForEach-Object { Write-Host "    $_" }
        throw "Build of $($Project.CsProj) failed with exit $LASTEXITCODE"
    }
    Write-Host " ok ($([int]$sw.Elapsed.TotalSeconds)s)" -ForegroundColor DarkGreen
}

function Get-ProjectDllPath {
    param([Parameter(Mandatory)] $Project)
    return (Join-Path $script:RepoRoot "artifacts\bin\$($Project.BinDir)\Debug\net10.0\$($Project.Dll)")
}

function Get-ProjectDllFilter {
    param([Parameter(Mandatory)] $Project)
    # CTS --filter is a glob relative to --rootPath (repo root).
    return "artifacts/bin/$($Project.BinDir)/Debug/net10.0/$($Project.Dll)"
}

function Assert-CleanRepo {
    Push-Location $script:RepoRoot
    try {
        $dirty = git status --porcelain
    } finally {
        Pop-Location
    }
    if ($dirty) {
        Write-Host "Working tree is dirty:" -ForegroundColor Red
        $dirty | ForEach-Object { Write-Host "  $_" }
        throw "CTS collect requires a clean working tree. Commit/stash first."
    }
}
