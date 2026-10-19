#requires -Version 5.1
<#
.SYNOPSIS
Tests installed .NET Framework MSBuild long-path handling without relocating its binaries.
.EXAMPLE
.\Test-MSBuildLongPaths.ps1 -MSBuildExe 'C:\Program Files\Microsoft Visual Studio\18\Preview\MSBuild\Current\Bin\MSBuild.exe'
.EXAMPLE
.\Test-MSBuildLongPaths.ps1 -MSBuildExe 'C:\Program Files\Microsoft Visual Studio\18\Preview\MSBuild\Current\Bin\MSBuild.exe' -TestFix
.NOTES
Read-only by default. -TestFix prompts before temporarily editing the selected executable's
configuration, normally requiring elevation. Close VS and other builds first. The original
configuration is backed up in the output directory and restored byte-for-byte in finally.
No NGEN images are generated, removed, or relocated.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)]
    [string] $MSBuildExe,
    [switch] $TestFix,
    [string] $OutputDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('MSBuildLongPaths-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)))
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$MSBuildExe = (Get-Item -LiteralPath $MSBuildExe).FullName
$config = "$MSBuildExe.config"
if ([IO.Path]::GetFileName($MSBuildExe) -ine 'MSBuild.exe' -or -not (Test-Path -LiteralPath $config -PathType Leaf)) {
    throw 'Select the installed .NET Framework MSBuild.exe and its adjacent .config, not dotnet/MSBuild.dll.'
}
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Output directory already exists. Choose a new -OutputDirectory: $OutputDirectory"
}
$OutputDirectory = [IO.Directory]::CreateDirectory($OutputDirectory).FullName
$fileName = ('f' * 60) + '.txt'
$originalBytes = [IO.File]::ReadAllBytes($config)
$originalTime = [IO.File]::GetLastWriteTimeUtc($config)
$backup = Join-Path $OutputDirectory 'MSBuild.exe.config.original'
[IO.File]::WriteAllBytes($backup, $originalBytes)
$originalConfig = [Convert]::ToBase64String($originalBytes)
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($MSBuildExe).ProductVersion
$policy = Get-ItemPropertyValue 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' -Name LongPathsEnabled
Write-Host "Executable: $MSBuildExe"
Write-Host "Version: $version; LongPathsEnabled=$policy"
Write-Host "Artifacts and original-config backup: $OutputDirectory"
if ($policy -ne 1) {
    Write-Warning 'The reporters had LongPathsEnabled=1. Enable it separately before drawing conclusions about this regression.'
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open((Join-Path $OutputDirectory 'input.zip'), [IO.Compression.ZipArchiveMode]::Create)
try {
    $writer = [IO.StreamWriter]::new($zip.CreateEntry($fileName).Open())
    try { $writer.Write('Long paths work.') } finally { $writer.Dispose() }
} finally { $zip.Dispose() }

$project = Join-Path $OutputDirectory 'repro.proj'
[IO.File]::WriteAllText($project, @'
<Project>
  <PropertyGroup>
    <LongDirectory>$(ReproRoot)\$(Padding)</LongDirectory>
    <LongFile>$(LongDirectory)\ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff.txt</LongFile>
  </PropertyGroup>
  <UsingTask TaskName="ReportLongPathState" TaskFactory="CodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
    <ParameterGroup><ReportFile ParameterType="System.String" Required="true" /></ParameterGroup>
    <Task>
      <Using Namespace="System" />
      <Using Namespace="System.IO" />
      <Using Namespace="System.Diagnostics" />
      <Using Namespace="System.Collections.Generic" />
      <Code Type="Fragment" Language="cs"><![CDATA[
        bool legacy, block;
        bool legacyFound = AppContext.TryGetSwitch("Switch.System.IO.UseLegacyPathHandling", out legacy);
        bool blockFound = AppContext.TryGetSwitch("Switch.System.IO.BlockLongPaths", out block);
        var lines = new List<string>();
        lines.Add("PID=" + Process.GetCurrentProcess().Id);
        lines.Add("Is64Bit=" + Environment.Is64BitProcess);
        lines.Add("TargetFramework=" + AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);
        lines.Add("UseLegacyPathHandling=" + legacy);
        lines.Add("BlockLongPaths=" + block);
        lines.Add("LegacyOverrideFound=" + legacyFound);
        lines.Add("BlockOverrideFound=" + blockFound);
        int nativeImages = 0;
        using (Process process = Process.GetCurrentProcess())
        {
            foreach (ProcessModule module in process.Modules)
            {
                if (module.FileName.EndsWith("MSBuild.ni.exe", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add("NativeImage=" + module.FileName);
                    nativeImages++;
                }
            }
        }
        lines.Add("NativeImageCount=" + nativeImages);
        foreach (string line in lines)
        {
            Log.LogMessage(Microsoft.Build.Framework.MessageImportance.High, line);
        }
        File.WriteAllLines(ReportFile, lines);
      ]]></Code>
    </Task>
  </UsingTask>
  <Target Name="Probe" BeforeTargets="Write;Extract">
    <Message Importance="High" Text="MSBuild=$(MSBuildVersion); Runtime=$(MSBuildRuntimeType); PathLength=$([System.String]::Copy('$(LongFile)').Length)" />
    <ReportLongPathState ReportFile="$(ReproRoot)\state.txt" />
  </Target>
  <Target Name="Write">
    <MakeDir Directories="$(LongDirectory)" />
    <WriteLinesToFile File="$(LongFile)" Lines="Long paths work." Overwrite="true" />
  </Target>
  <Target Name="Extract">
    <Unzip SourceFiles="$(MSBuildProjectDirectory)\input.zip" DestinationFolder="$(LongDirectory)" />
  </Target>
  <Target Name="Verify" AfterTargets="Write;Extract">
    <ReadLinesFromFile File="$(LongFile)"><Output TaskParameter="Lines" ItemName="OutputLines" /></ReadLinesFromFile>
    <Error Condition="'@(OutputLines)' != 'Long paths work.'" Text="Unexpected long-path file contents." />
    <WriteLinesToFile File="$(ReproRoot)\verified.txt" Lines="Verified" Overwrite="true" />
  </Target>
</Project>
'@)

$results = [Collections.Generic.List[object]]::new()
function Invoke-LongPathCase([string] $Phase, [string] $Target, [string] $ZapDisable) {
    $root = [IO.Directory]::CreateDirectory((Join-Path $OutputDirectory ("{0:d2}-{1}-{2}" -f $results.Count, $Phase, $Target))).FullName
    $paddingLength = 291 - $root.Length - $fileName.Length - 2
    if ($paddingLength -lt 1 -or $paddingLength -gt 255) {
        throw 'Choose a shorter -OutputDirectory so a valid 291-character file path can be constructed.'
    }
    $env:COMPLUS_ZapDisable = $ZapDisable
    Write-Host "`n=== $Phase / $Target / COMPLUS_ZapDisable=$ZapDisable ==="
    & $MSBuildExe $project '-nologo' '-noAutoResponse' '-nr:false' '-m:1' '-mt:false' '-v:minimal' `
        "-t:$Target" "-p:ReproRoot=$root" "-p:Padding=$('d' * $paddingLength)" `
        "-bl:$root\build.binlog" '-fl' "-flp:Verbosity=diagnostic;LogFile=$root\build.log" | Out-Host
    $exitCode = $LASTEXITCODE
    $state = @{}
    $report = Join-Path $root 'state.txt'
    if (Test-Path -LiteralPath $report) {
        foreach ($line in Get-Content -LiteralPath $report -Encoding UTF8) {
            $parts = $line -split '=', 2
            $state[$parts[0]] = $parts[1]
        }
    } else {
        Write-Warning "No process-state report was produced. Inspect $root\build.log before interpreting this failure."
    }
    [pscustomobject]@{
        Phase = $Phase
        Task = $(if ($Target -eq 'Write') { 'WriteLinesToFile' } else { 'Unzip' })
        ExitCode = $exitCode
        Verified = (Test-Path -LiteralPath (Join-Path $root 'verified.txt'))
        Legacy = $state['UseLegacyPathHandling']
        Block = $state['BlockLongPaths']
        NativeImageLoaded = $(if ($state.ContainsKey('NativeImageCount')) { [int]$state['NativeImageCount'] -gt 0 } else { $null })
        Is64Bit = $state['Is64Bit']
        PathLength = 291
        Log = (Join-Path $root 'build.log')
    }
}

$environmentNames = @('COMPLUS_ZapDisable', 'COMPLUS_JitMinOpts', 'MSBUILDUSESERVER', 'MSBUILDNOINPROCNODE', 'MSBUILDFORCEALLTASKSOUTOFPROC')
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:COMPLUS_JitMinOpts = '0'
    $env:MSBUILDUSESERVER = '0'
    $env:MSBUILDNOINPROCNODE = $null
    $env:MSBUILDFORCEALLTASKSOUTOFPROC = $null
    foreach ($target in @('Write', 'Extract')) { $results.Add((Invoke-LongPathCase 'Original' $target '0')) }
    foreach ($target in @('Write', 'Extract')) { $results.Add((Invoke-LongPathCase 'JIT' $target '1')) }

    if ($TestFix -and $PSCmdlet.ShouldProcess($config, 'Temporarily disable both legacy IO switches, run fresh processes, then restore the original configuration')) {
        if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($config)) -cne $originalConfig) {
            throw 'Configuration changed since the baseline. No workaround was applied.'
        }
        $access = [IO.File]::Open($config, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::Read)
        $access.Dispose()
        $document = [xml]::new()
        $document.PreserveWhitespace = $true
        $document.Load($config)
        $overrides = $document.SelectSingleNode('/configuration/runtime/AppContextSwitchOverrides')
        if ($null -eq $overrides) { throw 'Expected an existing AppContextSwitchOverrides element; no configuration was changed.' }
        $values = @($overrides.GetAttribute('value') -split ';' | Where-Object {
            $_ -and $_ -notmatch '^Switch\.System\.IO\.(UseLegacyPathHandling|BlockLongPaths)='
        })
        $overrides.SetAttribute('value', (($values + @('Switch.System.IO.UseLegacyPathHandling=false', 'Switch.System.IO.BlockLongPaths=false')) -join ';'))
        Write-Host "Original bytes backed up to $backup. Do not terminate this console during the temporary edit."
        $patchedConfig = $null
        try {
            $document.Save($config)
            $patchedConfig = [Convert]::ToBase64String([IO.File]::ReadAllBytes($config))
            foreach ($target in @('Write', 'Extract')) { $results.Add((Invoke-LongPathCase 'Patched' $target '0')) }
        } finally {
            if ($null -ne $patchedConfig -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($config)) -cne $patchedConfig) {
                throw "Configuration changed externally during the experiment. Refusing to overwrite it; original backup: $backup."
            }
            [IO.File]::WriteAllBytes($config, $originalBytes)
            [IO.File]::SetLastWriteTimeUtc($config, $originalTime)
            if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($config)) -cne $originalConfig) {
                throw "Original configuration was not restored. Restore it from $backup."
            }
            Write-Host 'Original configuration restored byte-for-byte.'
        }
        foreach ($target in @('Write', 'Extract')) { $results.Add((Invoke-LongPathCase 'Restored' $target '0')) }
    }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
}

if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($config)) -cne $originalConfig) {
    throw 'Executable configuration changed during the experiment.'
}
$results | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'results.json') -Encoding UTF8
$results | Format-Table Phase, Task, ExitCode, Verified, Legacy, Block, NativeImageLoaded, Is64Bit -AutoSize | Out-Host
Write-Host "All output paths were 291 characters. Logs, binlogs, process state, and results.json: $OutputDirectory"
Write-Host 'Real NGEN red/green requires Original failure -> Patched success -> Restored failure, with NativeImageLoaded=True throughout.'
Write-Host 'A passing Patched run that lost its native image may only be JIT fallback, not proof of an NGEN-preserving fix.'
