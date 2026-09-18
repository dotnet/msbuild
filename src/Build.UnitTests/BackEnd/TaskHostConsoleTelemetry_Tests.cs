// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public class TaskHostConsoleTelemetry_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void ConsoleSetupUsesNegotiatedVersionBeforeTaskConfiguration()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", Guid.NewGuid().ToString("N"));
        using BuildManager buildManager = new();
        StaleStartupVersionNodeLauncher launcher = new();
        ((IBuildComponentHost)buildManager).RegisterFactory(BuildComponentType.NodeLauncher, _ => launcher);
        MockLogger logger = new(_output);

        BuildResult result = buildManager.Build(
            new BuildParameters
            {
                MultiThreaded = true,
                MaxNodeCount = 1,
                EnableNodeReuse = false,
                Loggers = [logger],
            },
            CreateRequest(CreateProject(env, explicitTaskHost: false), standardError: false, text: "first task output"));

        result.ShouldHaveSucceeded();
        launcher.LaunchedTaskHost.ShouldBeTrue();
        int.Parse(result.ProjectStateAfterBuild!.GetPropertyValue("TaskHostProcessId"))
            .ShouldNotBe(EnvironmentUtilities.CurrentProcessId);
        AssertTelemetry(logger, expected: true);
    }

    private sealed class StaleStartupVersionNodeLauncher : INodeLauncher, IBuildComponent
    {
        private readonly NodeLauncher _launcher = new();

        public bool LaunchedTaskHost { get; private set; }

        public void InitializeComponent(IBuildComponentHost host) => _launcher.InitializeComponent(host);

        public void ShutdownComponent() => _launcher.ShutdownComponent();

        public Process Start(NodeLaunchData launchData, int nodeId)
        {
            string currentVersionArgument = $"/parentpacketversion:{NodePacketTypeExtensions.PacketVersion}";
            launchData.CommandLineArgs.ShouldContain("/nodemode:2");
            launchData.CommandLineArgs.ShouldContain(currentVersionArgument);
            LaunchedTaskHost = true;
            return _launcher.Start(launchData with
            {
                CommandLineArgs = launchData.CommandLineArgs.Replace(currentVersionArgument, "/parentpacketversion:6"),
            }, nodeId);
        }
    }
    [Fact]
    public void ReportsTelemetryForRepeatedAsynchronousBuilds()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
        env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", "1");
        using BuildManager buildManager = new();
        string projectFile = env.CreateFile("empty.proj", """
            <Project>
                <Target Name="Build" />
            </Project>
            """).Path;

        for (int iteration = 0; iteration < 100; iteration++)
        {
            MockLogger logger = new(_output);
            BuildResult result = buildManager.Build(
                new BuildParameters
                {
                    MaxNodeCount = 1,
                    EnableNodeReuse = false,
                    UseSynchronousLogging = false,
                    Loggers = [logger],
                },
                CreateRequest(projectFile, standardError: false, text: string.Empty));

            result.ShouldHaveSucceeded();
            AssertTelemetry(logger, expected: false);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ForwardsConsoleOutputFromBuildObjectDisposal(bool standardError, bool retainConnection)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", Guid.NewGuid().ToString("N"));
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", retainConnection ? null : ChangeWaves.Wave18_12.ToString());
        ChangeWaves.ResetStateForTests();
        using BuildManager buildManager = new();
        MockLogger logger = new(_output);

        BuildResult result = buildManager.Build(
            new BuildParameters
            {
                MultiThreaded = true,
                MaxNodeCount = 1,
                EnableNodeReuse = true,
                Loggers = [logger],
            },
            CreateRequest(CreateProject(env, explicitTaskHost: false), standardError, string.Empty, writeOnDispose: true));

        result.ShouldHaveSucceeded();
        env.WithTransientProcess(int.Parse(result.ProjectStateAfterBuild!.GetPropertyValue("TaskHostProcessId")));
        AssertTelemetry(logger, expected: true);
    }

    [Theory]
    [InlineData(false, false, false, "stdout", false)]
    [InlineData(true, false, false, "", false)]
    [InlineData(true, false, false, "stdout", true)]
    [InlineData(true, false, true, "stderr", true)]
    [InlineData(true, false, false, " ", true)]
    [InlineData(true, true, false, "stdout", false)]
    [InlineData(true, true, true, "stderr", false)]
    public void ReportsOnlyActualForwardedOutput(bool multiThreaded, bool explicitTaskHost, bool standardError, string text, bool expected)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using BuildManager buildManager = new();
        string projectFile = CreateProject(env, explicitTaskHost);
        MockLogger logger = new(_output);

        BuildResult result = buildManager.Build(
            new BuildParameters
            {
                MultiThreaded = multiThreaded,
                DisableInProcNode = false,
                MaxNodeCount = 1,
                EnableNodeReuse = false,
                Loggers = [logger],
            },
            CreateRequest(projectFile, standardError, text));

        result.ShouldHaveSucceeded();
        int processId = int.Parse(result.ProjectStateAfterBuild!.GetPropertyValue("TaskHostProcessId"));
        if (multiThreaded || explicitTaskHost)
        {
            processId.ShouldNotBe(EnvironmentUtilities.CurrentProcessId);
        }
        else
        {
            processId.ShouldBe(EnvironmentUtilities.CurrentProcessId);
        }

        AssertTelemetry(logger, expected);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public void ResetsBetweenBuildsWithReusedTaskHost(bool standardError, bool retainConnection, bool replacePooledProcess)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", Guid.NewGuid().ToString("N"));
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", retainConnection ? null : ChangeWaves.Wave18_12.ToString());
        ChangeWaves.ResetStateForTests();
        using BuildManager buildManager = new();
        string projectFile = CreateProject(env, explicitTaskHost: false);
        int? firstProcessId = null;
        HashSet<int> processIds = [];
        NodeProviderOutOfProcBase.NodeContext? firstConnection = null;

        (string Text, bool UseCachedWriter, bool Expected)[] builds =
        [
            ("first build output", false, true),
            ("stale writer output", true, false),
            ("", false, false),
            ("current writer output", false, true),
        ];

        foreach (var build in builds)
        {
            MockLogger logger = new(_output);
            BuildResult result = buildManager.Build(
                new BuildParameters
                {
                    MultiThreaded = true,
                    DisableInProcNode = false,
                    MaxNodeCount = 1,
                    EnableNodeReuse = true,
                    Loggers = [logger],
                },
                CreateRequest(projectFile, standardError, build.Text, build.UseCachedWriter));

            result.ShouldHaveSucceeded();
            int processId = int.Parse(result.ProjectStateAfterBuild!.GetPropertyValue("TaskHostProcessId"));
            processId.ShouldNotBe(EnvironmentUtilities.CurrentProcessId);
            bool firstUseOfProcess = processIds.Add(processId);
            if (firstUseOfProcess)
            {
                env.WithTransientProcess(processId);
            }

            if (firstProcessId is null)
            {
                firstProcessId = processId;
            }
            else if (retainConnection)
            {
                processId.ShouldBe(firstProcessId.Value);
            }

            if (replacePooledProcess)
            {
                firstUseOfProcess.ShouldBeTrue();
            }

            // Pool reuse is best-effort; a fresh process has no stale cached writer.
            AssertTelemetry(logger, build.Expected || (build.UseCachedWriter && firstUseOfProcess));

            NodeProviderOutOfProcTaskHost provider = ((IBuildComponentHost)buildManager)
                .GetComponent<NodeProviderOutOfProcTaskHost>(BuildComponentType.OutOfProcTaskHostNodeProvider);
            if (retainConnection)
            {
                NodeProviderOutOfProcBase.NodeContext connection = provider.ConnectedNodes.Values.ShouldHaveSingleItem();
                connection.ConnectionPersistsAcrossBuilds.ShouldBeTrue();
                firstConnection ??= connection;
                connection.ShouldBeSameAs(firstConnection);
            }
            else
            {
                provider.ConnectedNodes.ShouldBeEmpty();
            }

            provider.ConsoleOutputForwarded.ShouldBeFalse();
            provider.PacketReceived(1, new ConsoleWritePacket("late output", ConsoleOutput.Standard));
            provider.ConsoleOutputForwarded.ShouldBeFalse();

            if (replacePooledProcess)
            {
                using Process process = Process.GetProcessById(processId);
                process.Kill();
                bool exited = process.WaitForExit(10_000);
                try
                {
                    if (!exited && NativeMethodsShared.IsOSX)
                    {
                        WriteProcessExitDiagnostics(processId);
                    }
                }
                finally
                {
                    // Diagnostics must not turn the original timeout into a pass or a different failure.
                    exited.ShouldBeTrue();
                }
            }
        }
    }

    private void WriteProcessExitDiagnostics(int processId)
    {
        int parentProcessId = EnvironmentUtilities.CurrentProcessId;
        _output.WriteLine($"TaskHost {processId} did not exit after Kill and a 10-second wait. Parent test process: {parentProcessId}.");
        if (Traits.Instance.DebugUnitTests)
        {
            _output.WriteLine("Native diagnostics unavailable: the process runner disables timeouts in DebugUnitTests mode.");
            return;
        }

        RunDiagnostic("/bin/ps", $"-p {processId},{parentProcessId} -o pid,ppid,state,wchan,etime,comm");
        RunDiagnostic("/usr/bin/sample", $"{parentProcessId} 1 1 -file /dev/stdout");

        void RunDiagnostic(string executable, string arguments)
        {
            try
            {
                RunnerUtilities.RunProcessAndGetOutput(
                    executable,
                    arguments,
                    out bool successfulExit,
                    outputHelper: _output,
                    timeoutMilliseconds: 5_000);
                if (!successfulExit)
                {
                    _output.WriteLine($"Diagnostic command {executable} returned a nonzero exit code.");
                }
            }
            catch (Exception e) when (e is Win32Exception or TimeoutException or InvalidOperationException or IOException or AggregateException)
            {
                _output.WriteLine($"Diagnostic command {executable} failed: {e}");
            }
        }
    }

    private static string CreateProject(TestEnvironment env, bool explicitTaskHost)
    {
        return env.CreateFile("console-telemetry.proj", $"""
            <Project>
                <UsingTask TaskName="{nameof(ConsoleForwardingTelemetryTask)}"
                           AssemblyFile="{typeof(ConsoleForwardingTelemetryTask).Assembly.Location}"
                           {(explicitTaskHost ? """TaskFactory="TaskHostFactory" """ : "")} />
                <Target Name="Build">
                    <ConsoleForwardingTelemetryTask Text="$(Text)" StandardError="$(StandardError)" UseCachedWriter="$(UseCachedWriter)" WriteOnDispose="$(WriteOnDispose)">
                        <Output TaskParameter="ProcessId" PropertyName="TaskHostProcessId" />
                    </ConsoleForwardingTelemetryTask>
                </Target>
            </Project>
            """).Path;
    }

    private static BuildRequestData CreateRequest(string projectFile, bool standardError, string text, bool useCachedWriter = false, bool writeOnDispose = false)
    {
        return new BuildRequestData(
            projectFile,
            new Dictionary<string, string?>
            {
                ["Text"] = text,
                ["StandardError"] = standardError.ToString(),
                ["UseCachedWriter"] = useCachedWriter.ToString(),
                ["WriteOnDispose"] = writeOnDispose.ToString(),
            },
            null,
            ["Build"],
            null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);
    }

    private static void AssertTelemetry(MockLogger logger, bool expected)
    {
        TelemetryEventArgs telemetry = logger.TelemetryEvents.Where(e => e.EventName == "build").ShouldHaveSingleItem();
        telemetry.Properties[nameof(BuildTelemetry.TaskHostConsoleOutputForwarded)].ShouldBe(expected.ToString());
    }
}

public class ConsoleForwardingTelemetryTask : Utilities.Task
{
    private static TextWriter? s_firstWriter;

    public string Text { get; set; } = string.Empty;

    public bool StandardError { get; set; }

    public bool UseCachedWriter { get; set; }

    public bool WriteOnDispose { get; set; }

    [Output]
    public int ProcessId { get; set; }

    public override bool Execute()
    {
        ProcessId = EnvironmentUtilities.CurrentProcessId;
        if (WriteOnDispose)
        {
#pragma warning disable CA2000 // MSBuild disposes the registered build-lifetime object.
            ((IBuildEngine4)BuildEngine).RegisterTaskObject(
                nameof(ConsoleOnDispose), new ConsoleOnDispose(StandardError), RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
#pragma warning restore CA2000
        }

        TextWriter currentWriter = StandardError ? Console.Error : Console.Out;
        s_firstWriter ??= currentWriter;
        (UseCachedWriter ? s_firstWriter : currentWriter).Write(Text);
        return true;
    }

    private sealed class ConsoleOnDispose(bool standardError) : IDisposable
    {
        public void Dispose() => (standardError ? Console.Error : Console.Out).WriteLine("cleanup output");
    }
}
