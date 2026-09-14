// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public class TaskHostConsoleTelemetry_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

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
    [InlineData(false)]
    [InlineData(true)]
    public void ResetsBetweenBuildsWithReusedTaskHost(bool standardError)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using BuildManager buildManager = new();
        string projectFile = CreateProject(env, explicitTaskHost: false);
        int? firstProcessId = null;

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
            if (firstProcessId is null)
            {
                firstProcessId = processId;
                env.WithTransientProcess(processId);
            }
            else
            {
                processId.ShouldBe(firstProcessId.Value);
            }

            AssertTelemetry(logger, build.Expected);

            NodeProviderOutOfProcTaskHost provider = ((IBuildComponentHost)buildManager)
                .GetComponent<NodeProviderOutOfProcTaskHost>(BuildComponentType.OutOfProcTaskHostNodeProvider);
            provider.ConsoleOutputForwarded.ShouldBeFalse();
            provider.PacketReceived(1, new ServerNodeConsoleWrite("late output", ConsoleOutput.Standard));
            provider.ConsoleOutputForwarded.ShouldBeFalse();
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
                    <ConsoleForwardingTelemetryTask Text="$(Text)" StandardError="$(StandardError)" UseCachedWriter="$(UseCachedWriter)">
                        <Output TaskParameter="ProcessId" PropertyName="TaskHostProcessId" />
                    </ConsoleForwardingTelemetryTask>
                </Target>
            </Project>
            """).Path;
    }

    private static BuildRequestData CreateRequest(string projectFile, bool standardError, string text, bool useCachedWriter = false)
    {
        return new BuildRequestData(
            projectFile,
            new Dictionary<string, string?>
            {
                ["Text"] = text,
                ["StandardError"] = standardError.ToString(),
                ["UseCachedWriter"] = useCachedWriter.ToString(),
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

    [Output]
    public int ProcessId { get; set; }

    public override bool Execute()
    {
        ProcessId = EnvironmentUtilities.CurrentProcessId;
        TextWriter currentWriter = StandardError ? Console.Error : Console.Out;
        s_firstWriter ??= currentWriter;
        (UseCachedWriter ? s_firstWriter : currentWriter).Write(Text);
        return true;
    }
}
