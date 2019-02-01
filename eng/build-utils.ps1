
# Collection of powershell build utility functions that we use across our scripts.

Set-StrictMode -version 2.0
$ErrorActionPreference="Stop"

# Import Arcade functions
. (Join-Path $PSScriptRoot "common\tools.ps1")

$VSSetupDir = Join-Path $ArtifactsDir "VSSetup\$configuration"

function Exec-CommandCore([string]$command, [string]$commandArgs, [switch]$useConsole = $true) {
    if ($useConsole) {
        $exitCode = Exec-Process $command $commandArgs
        if ($exitCode -ne 0) {
            throw "Command failed to execute with exit code $($exitCode): $command $commandArgs"
        }
        return
    }

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $command
    $startInfo.Arguments = $commandArgs

    $startInfo.UseShellExecute = $false
    $startInfo.WorkingDirectory = Get-Location
    $startInfo.RedirectStandardOutput = $true
    $startInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $process.Start() | Out-Null

    $finished = $false
    try {
        # The OutputDataReceived event doesn't fire as events are sent by the
        # process in powershell.  Possibly due to subtlties of how Powershell
        # manages the thread pool that I'm not aware of.  Using blocking
        # reading here as an alternative which is fine since this blocks
        # on completion already.
        $out = $process.StandardOutput
        while (-not $out.EndOfStream) {
            $line = $out.ReadLine()
            Write-Output $line
        }

        while (-not $process.WaitForExit(100)) {
            # Non-blocking loop done to allow ctr-c interrupts
        }

        $finished = $true
        if ($process.ExitCode -ne 0) {
            throw "Command failed to execute with exit code $($process.ExitCode): $command $commandArgs"
        }
    }
    finally {
        # If we didn't finish then an error occured or the user hit ctrl-c.  Either
        # way kill the process
        if (-not $finished) {
            $process.Kill()
        }
    }
}

# Functions exactly like Exec-Command but lets the process re-use the current
# console. This means items like colored output will function correctly.
#
# In general this command should be used in place of
#   Exec-Command $msbuild $args | Out-Host
#
function Exec-Console([string]$command, [string]$commandArgs) {
    Exec-CommandCore -command $command -commandArgs $commandargs -useConsole:$true
}

function Get-VersionCore([string]$name, [string]$versionFile) {
    $name = $name.Replace(".", "")
    $name = $name.Replace("-", "")
    $nodeName = "$($name)Version"
    $x = [xml](Get-Content -raw $versionFile)

    $node = $x.SelectSingleNode("//Project/PropertyGroup/$nodeName")
    if ($node -ne $null) {
        return $node.InnerText
    }

    throw "Cannot find package $name in $versionFile"

}

# Return the version of the NuGet package as used in this repo
function Get-PackageVersion([string]$name) {
    return Get-VersionCore $name (Join-Path $EngRoot "Versions.props")
}

# Locate the directory where our NuGet packages will be deployed.  Needs to be kept in sync
# with the logic in Version.props
function Get-PackagesDir() {
    $d = $null
    if ($env:NUGET_PACKAGES -ne $null) {
        $d = $env:NUGET_PACKAGES
    }
    else {
        $d = Join-Path $env:UserProfile ".nuget\packages\"
    }

    Create-Directory $d
    return $d
}

# Locate the directory of a specific NuGet package which is restored via our main
# toolset values.
function Get-PackageDir([string]$name, [string]$version = "") {
    if ($version -eq "") {
        $version = Get-PackageVersion $name
    }

    $p = Get-PackagesDir
    $p = Join-Path $p $name.ToLowerInvariant()
    $p = Join-Path $p $version
    return $p
}