param (
    [Parameter(Mandatory = $false)]
    [string] $CPVSDrop
)

function CombineAndNormalize([string[]] $paths) {
    $combined = [System.IO.Path]::Combine($paths)
    return [System.IO.Path]::GetFullPath($combined)
}

function Log ($a) {
    Write-Host `n
    Write-Host $a.ToString()
}

class BuildInstance {
    static $languages = @("cs", "de", "en", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant")

    [string] $Root

    [string[]] $AssemblyNames = @(
        "Microsoft.Build.dll",
        "Microsoft.Build.Framework.dll",
        "Microsoft.Build.Tasks.Core.dll",
        "Microsoft.Build.Utilities.Core.dll"
    )

    [string[]] $SatelliteAssemblyNames = @(
        "Microsoft.Build.Conversion.Core.resources.dll",
        "Microsoft.Build.Engine.resources.dll",
        "Microsoft.Build.resources.dll",
        "Microsoft.Build.Tasks.Core.resources.dll",
        "Microsoft.Build.Utilities.Core.resources.dll",
        "MSBuild.resources.dll",
        "MSBuildTaskHost.resources.dll"
    )

    BuildInstance([String] $root) {
        $this.Root = $root
    }

    [string[]] BuildFiles() {
        return $this.ResolvedAssemblies() + $this.ResolvedSatelliteAssemblies()
    }

    [string[]] ResolvedAssemblies() {
        return $this.AssemblyNames | foreach{CombineAndNormalize($this.Root, $_)}
    }

    [string[]] ResolvedSatelliteAssemblies() {
        $satellites = @()

        if (-Not ($this.IsLocalized())) {
            return $satellites
        }

        foreach ($l in [BuildInstance]::languages) {
             foreach ($s in $this.SatelliteAssemblyNames) {
                $satellites += CombineAndNormalize(@($this.Root, $l, $s))
            }
        }

        return $satellites
    }

    [bool] IsLocalized() {
        return $this.Root -match ".*(x86|x64).*" 
    }

    [String] ToString() {
        return $this.Root + "`n`n" + (($this.BuildFiles() | foreach{"`t`t" + $_.ToString()}) -join "`n")
    }

    Check([Checker] $checker) {
        $checker.Check($this)
    }
}

class FullFrameworkBuildInstance : BuildInstance{
    FullFrameworkBuildInstance([String] $root) : base ($root) {
        ([BuildInstance]$this).AssemblyNames += @(
            "MSBuild.exe",
            "MSBuildTaskHost.exe",
            "Microsoft.Build.Conversion.Core.dll",
            "Microsoft.Build.Engine.dll"
        )
    }
}

class NetCoreBuildInstance : BuildInstance{
    NetCoreBuildInstance([String] $root) : base ($root) {
        ([BuildInstance]$this).AssemblyNames += "MSBuild.dll"
    }
}

class NugetPackage{
    [String] $Path

    NugetPackage([string] $Path) {
        $this.Path = $Path
    }

    Check([Checker] $checker) {
        $checker.Check($this)
    }

    [string] ToString() {
        return "NugetPackage: $($this.Path)"
    }
}

class VsixPackage{
    static [string] $PackageName = "Microsoft.Build.vsix"
    [String] $Path

    VsixPackage([string] $path) {
        $this.Path = CombineAndNormalize($path, [VsixPackage]::PackageName)
    }

    Check([Checker] $checker) {
        $checker.Check($this)
    }

    [string] ToString() {
        return "Vsix package: $($this.Path)"
    }
}

class Layout {

    [String] $FFx86;
    [String] $FFx64;
    [String] $FFAnyCPU;
    [String] $CoreAnyCPU;
    [String] $NugetPackagePath;
    [String] $VsixPath;

    [BuildInstance[]] $BuildInstances
    [NugetPackage[]] $NugetPackages
    [VsixPackage] $VsixPackage

    Layout($FFx86, $FFx64, $FFAnyCPU, $CoreAnyCPU, $NugetPackagePath, $VsixPath) {
        $this.FFx86 = $FFx86
        $this.FFx64 = $FFx64
        $this.FFAnyCPU = $FFAnyCPU
        $this.CoreAnyCPU = $CoreAnyCPU
        $this.NugetPackagePath = $NugetPackagePath
        $this.VsixPath = $VsixPath

        $this.BuildInstances = @( 
            [FullFrameworkBuildInstance]::new($this.FFx86),
            [FullFrameworkBuildInstance]::new($this.FFx64),
            [FullFrameworkBuildInstance]::new($this.FFAnyCPU),
            [NetCoreBuildInstance]::new($this.CoreAnyCPU)
        )

        $this.NugetPackages = Get-ChildItem $nugetPackagePath *.nupkg | foreach {[NugetPackage]::new($_.FullName)}
        $this.VsixPackage = [VsixPackage]::new($VsixPath)
    }

    [String] ToString() {
        $instances = ($this.BuildInstances | foreach{ "`t" + $_.ToString() }) -join "`n`n"
        $nugets = ($this.NugetPackages | foreach{"`t" + $_.ToString()}) -join "`n"
        return "Build Instances:`n$instances`n`nNuget Packages`n$($nugets)`n`n$($this.VsixPackage.ToString())"
    }

    static [Layout] FromMicrobuild() {
        $root = $env:BUILD_REPOSITORY_LOCALPATH

        $layout = [Layout]::new(
            (CombineAndNormalize @($root, $env:FFBinPath86)),
            (CombineAndNormalize @($root, $env:FFBinPath64)),
            (CombineAndNormalize @($root, $env:BinPath)),
            (CombineAndNormalize @($root, $env:BinPathNetCore)),
            (CombineAndNormalize @($root, $env:NugetPackagePathPath)),
            (CombineAndNormalize @($root, $env:SetupLayoutPath)))

        return $layout
    }

    static [Layout] FromCPVSDrop([string] $root) {

        $layout = [Layout]::new(
            (CombineAndNormalize @($root, "bin\x86\Windows_NT\Release\Output")),
            (CombineAndNormalize @($root, "bin\x64\Windows_NT\Release\Output")),
            (CombineAndNormalize @($root, "bin\Release\Output")),
            (CombineAndNormalize @($root, "bin\Release-NetCore\Output")),
            (CombineAndNormalize @($root, "bin\Packages")),
            (CombineAndNormalize @($root, "pkg")))

        return $layout
    }

    Check([Checker] $checker) {
        $checker.Check($this)

        $this.BuildInstances | foreach {$_.Check($checker)}
        $this.NugetPackages | foreach {$_.Check($checker)}
        $this.VsixPackage.Check($checker)
    }
}

class Diagnostic{
    [String] $Type
    [String] $Message

    Diagnostic([String] $type, [String] $message) {
        $this.Type = $type
        $this.Message = $message
    }
}

class Checker{
    [Diagnostic[]] $Diagnostics = @()

    Check($obj) {
        $diags = $this.HandleObject($obj)

        if ($diags -ne $null) {
            $this.Diagnostics += $diags
        }
    }

    [Diagnostic[]] HandleObject($obj) {
        $type = $obj.GetType()

        $diags = @()

        if ($type -eq [Layout]) {
            $diags = $this.CheckLayout($obj)
        }
        elseif ($type -eq [FullFrameworkBuildInstance]) {
            $diags = $this.CheckFullFrameworkBuildInstance($obj)
        }
        elseif ($type -eq [NetCoreBuildInstance]) {
            $diags = $this.CheckNetCoreBuildInstance($obj)
        }
        elseif ($type -eq [NugetPackage]) {
            $diags = $this.CheckNugetPackage($obj)
        }
        elseif ($type -eq [VsixPackage]) {
            $diags = $this.CheckVSixPackage($obj)
        }
        else {
            $diags =  $this.HandleUnknownType($obj, $type)
        }

        return $diags
    }

    [Diagnostic[]] HandleUnknownType($obj, [Type] $type) {
        throw [System.NotImplementedException]
    }

    [Diagnostic[]] CheckLayout([Layout] $l) {return @()}
    [Diagnostic[]] CheckFullFrameworkBuildInstance([FullFrameworkBuildInstance] $b) {return @()}
    [Diagnostic[]] CheckNetCoreBuildInstance([NetCoreBuildInstance] $b) {return @()}
    [Diagnostic[]] CheckNugetPackage([NugetPackage] $n) {return @()}
    [Diagnostic[]] CheckVSixPackage([VsixPackage] $v) {return @()}

    [Diagnostic] NewDiagnostic([String] $message) {
        return [Diagnostic]::new($this.GetType().Name, $message)
    }
}

class TestChecker : Checker{
    [Diagnostic[]] CheckLayout([Layout] $l) {return $this.NewDiagnostic("Checked Layout")}
    [Diagnostic[]] CheckFullFrameworkBuildInstance([FullFrameworkBuildInstance] $b) {return $this.NewDiagnostic("Checked FF Build Instance: $($b.Root)")}
    [Diagnostic[]] CheckNetCoreBuildInstance([NetCoreBuildInstance] $b) {return $this.NewDiagnostic("Checked Core Build Instance: $($b.Root)")}
    [Diagnostic[]] CheckNugetPackage([NugetPackage] $n) {return $this.NewDiagnostic("Checked Nuget Package: $($n.Path)")}
    [Diagnostic[]] CheckVSixPackage([VsixPackage] $v) {return $this.NewDiagnostic("Checked VsixPackage: $($v.Path)")}
}

class FileChecker : Checker{
    $ExpectedNumberOfNugetPackages = 7

    [Diagnostic[]] CheckPathExists([string] $path) {
        if (-Not (Test-Path $path)) {
            return $this.NewDiagnostic("Path does not exist: $path");
        }

        return @()
    }

    [Diagnostic[]] CheckLayout([Layout] $l) {
        $diags = @() 

        $diags += $this.CheckPathExists($l.FFx86)
        $diags += $this.CheckPathExists($l.FFx64)
        $diags += $this.CheckPathExists($l.FFAnyCPU)
        $diags += $this.CheckPathExists($l.CoreAnyCPU)
        $diags += $this.CheckPathExists($l.NugetPackagePath)
        $diags += $this.CheckPathExists($l.VsixPath)

        if ($l.NugetPackages.Count -ne $this.ExpectedNumberOfNugetPackages) {
            $diags += $this.NewDiagnostic("There should be $($this.ExpectedNumberOfNugetPackages) nuget packages in $($l.NugetPackagePath) but $($l.NugetPackages.Count) were found")
        }

        return $diags
    }

    [Diagnostic[]] CheckBuildInstance([BuildInstance] $b) {
        return $b.BuildFiles() | foreach{$this.CheckPathExists($_)}
    }

    [Diagnostic[]] CheckFullFrameworkBuildInstance([FullFrameworkBuildInstance] $b) {
        return $this.CheckBuildInstance($b)
    }
    
    [Diagnostic[]] CheckNetCoreBuildInstance([NetCoreBuildInstance] $b) {
        return $this.CheckBuildInstance($b)
    }

    [Diagnostic[]] CheckNugetPackage([NugetPackage] $n) {
        return $this.CheckPathExists($n.Path)
    }

    [Diagnostic[]] CheckVSixPackage([VsixPackage] $v) {
        return $this.CheckPathExists($v.Path)
    }
}

class RealSignedChecker : Checker {
    [Diagnostic[]] CheckIsSigned([String] $assembly) {
        $signature = Get-AuthenticodeSignature $assembly

        $looksSigned = $signature.Status -eq [System.Management.Automation.SignatureStatus]::Valid
        $looksSigned = $looksSigned -and ($signature.SignatureType -eq [System.Management.Automation.SignatureType]::Authenticode)
        $looksSigned = $looksSigned -and ($signature.SignerCertificate.Issuer -match ".*Microsoft.*Redmond.*")

        if (-Not $looksSigned) {
            return $this.NewDiagnostic("Assembly not signed: $assembly")
        }

        return @()
    }

    [Diagnostic[]] CheckBuildInstance([BuildInstance] $b) {
        return $b.BuildFiles() | foreach{$this.CheckIsSigned($_)}
    }

    [Diagnostic[]] CheckFullFrameworkBuildInstance([FullFrameworkBuildInstance] $b) {
        return $this.CheckBuildInstance($b)
    }

    [Diagnostic[]] CheckNetCoreBuildInstance([NetCoreBuildInstance] $b) {
        return $this.CheckBuildInstance($b)
    }
}

class NugetVersionChecker : Checker {
    [Diagnostic[]] CheckNugetPackage([NugetPackage] $n) {
        $packageNameRegex = "Microsoft\.Build\..*\d+\.\d+\.\d+.*\.nupkg"
        $packageName = [System.IO.Path]::GetFileName($n.Path)

        if (-Not ($packageName -match $packageNameRegex)) {
            return $this.NewDiagnostic("Package `"$packageName`" does not match regex `"$packageNameRegex`"")
        }

        return @()
    }
}

[String[]] $diagnostics = @()

$layout = $null

if ($CPVSDrop) {
    Log "Used `$CPVSDrop=$CPVSDrop"
    $layout = [Layout]::FromCPVSDrop($CPVSDrop)
}
else {
    Log "Running inside microbuild environment"
    $layout = [Layout]::FromMicrobuild()
}

Log $layout

# $checkers = @([TestChecker]::new())
$checkers = @(
    [FileChecker]::new(),
    [RealSignedChecker]::new(),
    [NugetVersionChecker]::new()
    )

$checkers | foreach{$layout.Check($_)}

$diagnosticCount = 0

Log "Failed checks:"

foreach ($checker in $checkers) {
    $diags = $checker.Diagnostics

    $diagnosticCount += $diags.Count

    $diags | foreach{Log "$($_.Type): $($_.Message)"}
}

if ($diagnosticCount -eq 0) {
    Log "No failed checks"
}
else {
    Log "$diagnosticCount failed checks"
}
