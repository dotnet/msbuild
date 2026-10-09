// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class BootstrapBuildSwitches_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [WindowsOnlyFact]
    public void BootstrapDailyWindowsSwitchesOnlyChangeFinalStage()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateWrapperEnvironment(env, windows: true);

        RunWindowsWrapper(root, "-bootstrapDaily -bootstrapSdkChannel 12.0.1xx", out bool success).ShouldContain("Stage 1 build");
        success.ShouldBeTrue();
        File.ReadAllLines(Path.Combine(root, "calls.txt")).ShouldBe(["/p:UseDailyBootstrapSdk=true|/p:BootstrapSdkChannel=12.0.1xx"]);
        File.Delete(Path.Combine(root, "calls.txt"));

        RunWindowsWrapper(root, "-stage2 -msbuildEngine dotnet -bootstrapDaily -bootstrapSdkChannel 12.0.1xx", out success);
        success.ShouldBeTrue();
        File.ReadAllLines(Path.Combine(root, "calls.txt")).ShouldBe(
            ["/p:UseDailyBootstrapSdk=false", "/p:UseDailyBootstrapSdk=true|/p:BootstrapSdkChannel=12.0.1xx"]);
    }

    [WindowsOnlyFact]
    public void BootstrapDailyWindowsHelpDoesNotStartStageTwo()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateWrapperEnvironment(env, windows: true);

        string log = RunWindowsWrapper(root, "-help -stage2", out bool success);

        success.ShouldBeTrue(log);
        log.ShouldContain("-bootstrapDaily");
        log.ShouldContain("-bootstrapSdkChannel");
        Directory.Exists(Path.Combine(root, "stage1")).ShouldBeFalse();
    }

    [WindowsOnlyFact]
    public void BootstrapDailyWindowsChannelRequiresDailyMode()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateWrapperEnvironment(env, windows: true);

        string log = RunWindowsWrapper(root, "-bootstrapSdkChannel 12.0.1xx", out bool success);

        success.ShouldBeFalse(log);
        log.ShouldContain("-bootstrapDaily");
        File.Exists(Path.Combine(root, "calls.txt")).ShouldBeFalse();
    }

    [UnixOnlyFact]
    public void BootstrapDailyUnixSwitchesOnlyChangeFinalStage()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateWrapperEnvironment(env, windows: false);

        RunUnixWrapper(root, "--bootstrap-daily --bootstrap-sdk-channel 12.0.1xx", out bool success);
        success.ShouldBeTrue();
        File.ReadAllLines(Path.Combine(root, "calls.txt")).ShouldBe(["/p:UseDailyBootstrapSdk=true|/p:BootstrapSdkChannel=12.0.1xx"]);
        File.Delete(Path.Combine(root, "calls.txt"));

        RunUnixWrapper(root, "--stage2 --bootstrap-daily --bootstrap-sdk-channel 12.0.1xx", out success);
        success.ShouldBeTrue();
        File.ReadAllLines(Path.Combine(root, "calls.txt")).ShouldBe(
            ["/p:UseDailyBootstrapSdk=false", "/p:UseDailyBootstrapSdk=true|/p:BootstrapSdkChannel=12.0.1xx"]);
    }

    [UnixOnlyFact]
    public void BootstrapDailyUnixHelpDoesNotStartStageTwo()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateWrapperEnvironment(env, windows: false);

        string log = RunUnixWrapper(root, "--help --stage2", out bool success);

        success.ShouldBeTrue(log);
        log.ShouldContain("--bootstrap-daily");
        log.ShouldContain("--bootstrap-sdk-channel");
        Directory.Exists(Path.Combine(root, "stage1")).ShouldBeFalse();
    }

    [UnixOnlyFact]
    public void BootstrapDailyUnixChannelRequiresDailyMode()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateWrapperEnvironment(env, windows: false);

        string log = RunUnixWrapper(root, "--bootstrap-sdk-channel 12.0.1xx", out bool success);

        success.ShouldBeFalse(log);
        log.ShouldContain("--bootstrap-daily");
        File.Exists(Path.Combine(root, "calls.txt")).ShouldBeFalse();
    }

    private string RunWindowsWrapper(string root, string arguments, out bool success)
        => RunnerUtilities.RunProcessAndGetOutput(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(root, "eng", "build.ps1")}\" {arguments}",
            out success,
            outputHelper: _output);

    private string RunUnixWrapper(string root, string arguments, out bool success)
        => RunnerUtilities.RunProcessAndGetOutput(
            "/bin/bash",
            $"\"{Path.Combine(root, "eng", "build.sh")}\" {arguments}",
            out success,
            outputHelper: _output);

    private static string CreateWrapperEnvironment(TestEnvironment env, bool windows)
    {
        env.SetEnvironmentVariable("_BuildToolPath", null);
        env.SetEnvironmentVariable("_BuildToolCommand", null);
        string root = env.CreateFolder(subfolder: "build wrapper with spaces").Path;
        string eng = Path.Combine(root, "eng");
        string common = Path.Combine(eng, "common");
        Directory.CreateDirectory(common);
        string extension = windows ? "ps1" : "sh";
        string repoRoot = Path.GetFullPath(Path.Combine(RunnerUtilities.BootstrapRootPath, "..", "..", ".."));
        File.Copy(Path.Combine(repoRoot, "eng", $"build.{extension}"), Path.Combine(eng, $"build.{extension}"));
        File.WriteAllText(Path.Combine(root, "global.json"), """{"tools":{"dotnet":"11.0.100"}}""");
        File.WriteAllText(Path.Combine(common, $"build.{extension}"), windows
            ? """
              [CmdletBinding(PositionalBinding=$false)]
              param(
                [string]$configuration, [string]$msbuildEngine,
                [switch]$prepareMachine, [switch]$disablePipelineSetResult, [switch]$help,
                [Parameter(ValueFromRemainingArguments=$true)][string[]]$properties)
              $repo = Join-Path $PSScriptRoot '..\..'
              if (-not $help) {
                [IO.Directory]::CreateDirectory((Join-Path $repo 'artifacts')) | Out-Null
                [IO.File]::AppendAllText((Join-Path $repo 'calls.txt'), ($properties -join '|') + "`n")
              }
              exit 0
              """
            : """
              #!/usr/bin/env bash
              repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
              properties=()
              while [[ $# -gt 0 ]]; do
                case "$1" in
                  --configuration) shift 2 ;;
                  --prepareMachine|--disablePipelineSetResult) shift ;;
                  --help|-h) exit 0 ;;
                  *) properties+=("$1"); shift ;;
                esac
              done
              mkdir -p "$repo/artifacts"
              (IFS='|'; printf '%s\n' "${properties[*]}") >> "$repo/calls.txt"
              exit 0
              """);
        return root;
    }
}
