// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests;

public sealed class MSBuildClientApp_Tests
{
    private readonly ITestOutputHelper _output;

    public MSBuildClientApp_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BusyResidentUsesTransientServer(bool launchContention, bool buildFails)
    {
        using TestEnvironment env = CreateEnvironment();
        ServerNodeHandshake handshake = CreateResidentHandshake();
        using Mutex? running = launchContention
            ? null
            : new(initiallyOwned: false, $@"Global\msbuild-server-running-{handshake.ComputeHash()}");
        using Mutex contention = HoldResidentContention(handshake, launchContention);
        TransientTestFile result = env.CreateFile(".txt");
        TransientTestFile binlog = env.CreateFile(".binlog");
        TransientTestFile project = CreateProbeProject(env, result.Path);

        MSBuildClientApp.Execute(
            ["MSBuild.exe", project.Path, "-mt", "-nodeReuse:true", $"-p:FailBuild={buildFails}", $"-bl:{binlog.Path}"],
            RunnerUtilities.BootstrapMSBuildExecutablePath,
            multiThreaded: true,
            shutdownServerAfterBuild: false,
            CancellationToken.None).ShouldBe(buildFails ? MSBuildApp.ExitType.BuildError : MSBuildApp.ExitType.Success);

        string[] probe = File.ReadAllText(result.Path).Trim().Split('|');
        int serverPid = int.Parse(probe[0]);
        serverPid.ShouldNotBe(Environment.ProcessId, "The busy resident must not force the MT build into its client.");
        env.WithTransientProcess(serverPid);
        if (Environment.ProcessorCount > 1)
        {
            bool.Parse(probe[1]).ShouldBeTrue("The private server must launch with Server GC.");
        }

        MSBuildServerLifecycleEventArgs lifecycle = ReadLifecycle(binlog.Path);
        lifecycle.Kind.ShouldBe(MSBuildServerLifecycleKind.Spawned);
        lifecycle.ShortLived.ShouldBeTrue();
        lifecycle.ProcessId.ShouldBe(serverPid);
        lifecycle.ReasonCode.ShouldBeNull();
        WaitForProcessExit(serverPid)
            .ShouldBeTrue("The overflow server must exit even though the build requested node reuse.");
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void BusyResidentRetainsInProcessFallback(bool launchContention, bool multiThreaded, bool cancelled)
    {
        using TestEnvironment env = CreateEnvironment();
        ServerNodeHandshake handshake = CreateResidentHandshake();
        using Mutex? running = launchContention
            ? null
            : new(initiallyOwned: false, $@"Global\msbuild-server-running-{handshake.ComputeHash()}");
        using Mutex contention = HoldResidentContention(handshake, launchContention);
        TransientTestFile result = env.CreateFile(".txt");
        TransientTestFile binlog = env.CreateFile(".binlog");
        TransientTestFile project = CreateProbeProject(env, result.Path);
        string missingMSBuild = Path.Combine(env.CreateFolder().Path, "missing", "MSBuild.exe");
        using CancellationTokenSource cancellation = new();
        if (cancelled)
        {
            cancellation.Cancel();
        }

        MSBuildClientApp.Execute(
            ["MSBuild.exe", project.Path, $"-mt:{multiThreaded}", "-nodeReuse:true", $"-bl:{binlog.Path}"],
            missingMSBuild,
            multiThreaded,
            shutdownServerAfterBuild: false,
            cancellation.Token).ShouldBe(MSBuildApp.ExitType.Success);

        string[] probe = File.ReadAllText(result.Path).Trim().Split('|');
        int.Parse(probe[0]).ShouldBe(Environment.ProcessId);
        MSBuildServerLifecycleEventArgs lifecycle = ReadLifecycle(binlog.Path);
        lifecycle.Kind.ShouldBe(MSBuildServerLifecycleKind.NotUsed);
        lifecycle.ReasonCode.ShouldBe(multiThreaded && !cancelled
            ? MSBuildApp.ServerNotUsedReasonCodeServerCrashed
            : MSBuildApp.ServerNotUsedReasonCodeServerBusy);
    }

    private TestEnvironment CreateEnvironment()
    {
        TestEnvironment env = TestEnvironment.Create(_output);
        foreach (var variable in RunnerUtilities.GetBootstrapMSBuildEnvironmentVariables())
        {
            env.SetEnvironmentVariable(variable.Key, variable.Value);
        }

        // The tools root participates in the handshake, so the in-process client must use the server's layout.
        BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(new BuildEnvironment(
            BuildEnvironmentMode.Standalone,
            RunnerUtilities.BootstrapMSBuildExecutablePath,
            runningTests: true,
            runningInMSBuildExe: false,
            runningInVisualStudio: false,
            visualStudioPath: null));

        env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", Guid.NewGuid().ToString("N"));
        env.SetEnvironmentVariable("DOTNET_gcServer", null);
        env.SetEnvironmentVariable("COMPlus_gcServer", null);
        env.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", null);
        return env;
    }

    private static ServerNodeHandshake CreateResidentHandshake() => new(
        CommunicationsUtilities.GetHandshakeOptions(
            taskHost: false,
            taskHostParameters: TaskHostParameters.Empty,
            architectureFlagToSet: XMakeAttributes.GetCurrentMSBuildArchitecture()));

    private static Mutex HoldResidentContention(ServerNodeHandshake handshake, bool launchContention) =>
        new(
            initiallyOwned: false,
            launchContention
                ? $@"Global\msbuild-server-launch-{handshake.ComputeHash()}"
                : $@"Global\msbuild-server-busy-{handshake.ComputeHash()}");

    private static TransientTestFile CreateProbeProject(TestEnvironment env, string resultPath) =>
        env.CreateFile("probe.proj", $"""
            <Project>
              <UsingTask TaskName="ProcessIdTask" AssemblyFile="{Assembly.GetExecutingAssembly().Location}" />
              <Target Name="Probe">
                <ProcessIdTask>
                  <Output PropertyName="PID" TaskParameter="Pid" />
                  <Output PropertyName="SERVERGC" TaskParameter="IsServerGC" />
                </ProcessIdTask>
                <WriteLinesToFile File="{resultPath}" Lines="$(PID)|$(SERVERGC)" />
                <Error Text="Expected test failure" Condition="'$(FailBuild)' == 'true'" />
              </Target>
            </Project>
            """);

    private static MSBuildServerLifecycleEventArgs ReadLifecycle(string binlog)
    {
        List<MSBuildServerLifecycleEventArgs> events = [];
        BinaryLogReplayEventSource replay = new();
        replay.AnyEventRaised += (_, args) =>
        {
            if (args is MSBuildServerLifecycleEventArgs lifecycle)
            {
                events.Add(lifecycle);
            }
        };
        replay.Replay(binlog);
        return events.ShouldHaveSingleItem();
    }

    private static bool WaitForProcessExit(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return process.WaitForExit(10_000);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}
#endif
