#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

[CmdletBinding(PositionalBinding=$false)]
Param(
  [string][Alias('c')]$configuration = "Debug",
  [string] $projects,
  [string][Alias('v')]$verbosity = "minimal",
  [string] $msbuildEngine = $null,
  [bool] $warnAsError = $true,
  [bool] $nodeReuse = $true,
  [switch] $execute,
  [switch][Alias('r')]$restore,
  [switch] $deployDeps,
  [switch][Alias('b')]$build,
  [switch] $rebuild,
  [switch] $deploy,
  [switch] $test,
  [switch] $integrationTest,
  [switch] $performanceTest,
  [switch] $sign,
  [switch] $pack,
  [switch] $publish,
  [switch] $publishBuildAssets,
  [switch][Alias('bl')]$binaryLog,
  [switch] $ci,
  [switch] $prepareMachine,
  [switch] $help,

  # official build settings
  [string]$officialBuildId = "",
  [string]$vsDropName = "",
  [string]$vsBranch = "",
  [string]$vsDropAccessToken = "",

  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

. (Join-Path $PSScriptRoot "build-utils.ps1")

function Print-Usage() {
    Write-Host "Common settings:"
    Write-Host "  -configuration <value>  Build configuration: 'Debug' or 'Release' (short: -c)"
    Write-Host "  -verbosity <value>      Msbuild verbosity: q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic] (short: -v)"
    Write-Host "  -binaryLog              Output binary log (short: -bl)"
    Write-Host "  -help                   Print help and exit"
    Write-Host ""

    Write-Host "Actions:"
    Write-Host "  -restore                Restore dependencies (short: -r)"
    Write-Host "  -build                  Build solution (short: -b)"
    Write-Host "  -rebuild                Rebuild solution"
    Write-Host "  -deploy                 Deploy built VSIXes"
    Write-Host "  -deployDeps             Deploy dependencies (e.g. VSIXes for integration tests)"
    Write-Host "  -test                   Run all unit tests in the solution"
    Write-Host "  -pack                   Package build outputs into NuGet packages and Willow components"
    Write-Host "  -integrationTest        Run all integration tests in the solution"
    Write-Host "  -performanceTest        Run all performance tests in the solution"
    Write-Host "  -sign                   Sign build outputs"
    Write-Host "  -publish                Publish artifacts (e.g. symbols)"
    Write-Host "  -publishBuildAssets     Push assets to BAR"
    Write-Host ""

    Write-Host "Official build settings:"
    Write-Host "  -officialBuildId          An official build id, e.g. 20190102.3"
    Write-Host "  -vsDropName               Visual Studio product drop name"
    Write-Host "  -vsBranch                 Visual Studio insertion branch"
    Write-Host "  -vsDropAccessToken        Visual Studio drop access token"
    Write-Host ""

    Write-Host "Advanced settings:"
    Write-Host "  -projects <value>       Semi-colon delimited list of sln/proj's to build. Globbing is supported (*.sln)"
    Write-Host "  -ci                     Set when running on CI server"
    Write-Host "  -prepareMachine         Prepare machine for CI run"
    Write-Host "  -msbuildEngine <value>  Msbuild engine to use to run build ('dotnet', 'vs', or unspecified)."
    Write-Host ""
    Write-Host "Command line arguments not listed above are passed thru to msbuild."
    Write-Host "The above arguments can be shortened as much as to be unambiguous (e.g. -co for configuration, -t for test, etc.)."
}

function Process-Arguments() {
    if ($help -or (($properties -ne $null) -and ($properties.Contains("/help") -or $properties.Contains("/?")))) {
       Print-Usage
       exit 0
    }

    if (!$vsBranch) {
        if ($officialBuildId) {
            Write-Host "vsBranch must be specified for official builds"
            exit 1
        }

        $script:vsBranch = "dummy/ci"
    }

    if (!$vsDropName) {
        if ($officialBuildId) {
            Write-Host "vsDropName must be specified for official builds"
            exit 1
        }

        $script:vsDropName = "Products/DummyDrop"
    }

    if (!$vsDropAccessToken -and $officialBuildId) {
        Write-Host "vsDropAccessToken must be specified for official builds"
        exit 1
    }
}

function InitializeCustomToolset {
  if (-not $restore) {
    return
  }

  $script = Join-Path $EngRoot "restore-toolset.ps1"

  if (Test-Path $script) {
    . $script
  }
}

function Build-Repo() {
  $toolsetBuildProj = InitializeToolset
  InitializeCustomToolset
  $bl = if ($binaryLog) { "/bl:" + (Join-Path $LogDir "Build.binlog") } else { "" }

  if ($projects) {
    # Re-assign properties to a new variable because PowerShell doesn't let us append properties directly for unclear reasons.
    # Explicitly set the type as string[] because otherwise PowerShell would make this char[] if $properties is empty.
    [string[]] $msbuildArgs = $properties
    $msbuildArgs += "/p:Projects=$projects"
    $properties = $msbuildArgs
  }

  $optDataDir = if ($applyOptimizationData) { $IbcOptimizationDataDir } else { "" }

  MSBuild $toolsetBuildProj `
    $bl `
    /p:Configuration=$configuration `
    /p:RepoRoot=$RepoRoot `
    /p:Restore=$restore `
    /p:DeployDeps=$deployDeps `
    /p:Build=$build `
    /p:Rebuild=$rebuild `
    /p:Deploy=$deploy `
    /p:Test=$test `
    /p:Pack=$pack `
    /p:IntegrationTest=$integrationTest `
    /p:PerformanceTest=$performanceTest `
    /p:Sign=$sign `
    /p:Publish=$publish `
    /p:Execute=$execute `
    /p:ContinuousIntegrationBuild=$ci `
    /p:VisualStudioDropName=$vsDropName `
    /p:IbcOptimizationDataDir=$optDataDir `
    /p:OfficialBuildId=$officialBuildId `
    @properties
}

function Restore-OptProfData() {
    $dropToolDir = Get-PackageDir "Drop.App"
    $dropToolPath = Join-Path $dropToolDir "lib\net45\drop.exe"

    if (!(Test-Path $dropToolPath)) {

        # Only report error when running in an official build.
        # Allows to test optimization data operations locally by running
        # cibuild.cmd after manually restoring internal tools project.
        if (!$officialBuildId) {
            $script:applyOptimizationData = $false
            return
        }

        Write-Host "Internal tool not found: '$dropToolPath'." -ForegroundColor Red
        Write-Host "Run nuget restore `"$EngRoot\internal\Toolset.csproj`"." -ForegroundColor DarkGray
        ExitWithExitCode 1
    }

    function find-latest-drop($drops) {
         $result = $null
         [DateTime]$latest = [DateTime]::New(0)
         foreach ($drop in $drops) {
             $dt = [DateTime]::Parse($drop.CreatedDateUtc)
             if ($result -eq $null -or ($drop.UploadComplete -and !$drop.DeletePending -and ($dt -gt $latest))) {
                 $result = $drop
                 $latest = $dt
             }
         }

         return $result
    }

    Write-Host "Acquiring optimization data"

    Create-Directory $IbcOptimizationDataDir

    $dropServiceUrl = "https://devdiv.artifacts.visualstudio.com"
    # The branh name here needs to be parameterized.
    $dropNamePrefix = "OptimizationData/microsoft/MSBuild/vs16.0"
    $patAuth = if ($officialBuildId) { "--patAuth `"$vsDropAccessToken`"" } else { "" }

    $dropsJsonPath = Join-Path $IbcOptimizationDataDir "AvailableDrops.json"
    $logFile = Join-Path $LogDir "OptimizationDataAcquisition.log"

    Exec-Console $dropToolPath "list --dropservice `"$dropServiceUrl`" $patAuth --pathPrefixFilter `"$dropNamePrefix`" --toJsonFile `"$dropsJsonPath`" --traceto `"$logFile`""
    $dropsJson = Get-Content -Raw -Path $dropsJsonPath | ConvertFrom-Json
    $latestDrop = find-latest-drop($dropsJson)

    if ($latestDrop -eq $null) {
        Write-Host "No drop matching given name found: $dropServiceUrl/$dropNamePrefix/*" -ForegroundColor Red
        ExitWithExitCode 1
    }

    Write-Host "Downloading optimization data from drop $dropServiceUrl/$($latestDrop.Name)"
    Exec-Console $dropToolPath "get --dropservice `"$dropServiceUrl`" $patAuth --name `"$($latestDrop.Name)`" --dest `"$IbcOptimizationDataDir`" --traceto `"$logFile`""
}

function Build-OptProfData() {
    $insertionDir = Join-Path $VSSetupDir "Insertion"

    $optProfDir = Join-Path $ArtifactsDir "OptProf\$configuration"
    $optProfDataDir = Join-Path $optProfDir "Data"
    $optProfBranchDir = Join-Path $optProfDir "BranchInfo"

    $optProfConfigFile = Join-Path $EngRoot "config\OptProf.json"
    $optProfToolDir = Get-PackageDir "RoslynTools.OptProf"
    $optProfToolExe = Join-Path $optProfToolDir "tools\roslyn.optprof.exe"

    # This invocation is failing right now. Going to assume we don't need it at the moment.
    Write-Host "Generating optimization data using '$optProfConfigFile' into '$optProfDataDir'"
    Exec-Console $optProfToolExe "--configFile $optProfConfigFile --insertionFolder $insertionDir --outputFolder $optProfDataDir"

    # Write out branch we are inserting into
    Create-Directory $optProfBranchDir
    $vsBranchFile = Join-Path $optProfBranchDir "vsbranch.txt"
    $vsBranch >> $vsBranchFile

    # Set VSO variables used by MicroBuildBuildVSBootstrapper pipeline task
    $manifestList = [string]::Join(',', (Get-ChildItem "$insertionDir\*.vsman"))

    Write-Host "##vso[task.setvariable variable=VisualStudio.SetupManifestList;]$manifestList"
}

try {
  Process-Arguments

  if ($ci) {
    $binaryLog = $true
    $nodeReuse = $false
  }

  # Import custom tools configuration, if present in the repo.
  # Note: Import in global scope so that the script set top-level variables without qualification.
  $configureToolsetScript = Join-Path $EngRoot "configure-toolset.ps1"
  if (Test-Path $configureToolsetScript) {
    . $configureToolsetScript
  }

  # IBC merge is only invoked in official build, but we want to enable running IBCMerge locally as well.
  $applyOptimizationData = $ci -and $configuration -eq "Release" -and $msbuildEngine -eq "vs"

  if ($applyOptimizationData -and $restore) {
    Restore-OptProfData
  }

  Build-Repo

  if ($applyOptimizationData -and $build) {
    Build-OptProfData
  }
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  ExitWithExitCode 1
}

ExitWithExitCode 0