// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd;

public class TaskHostEnvironment_E2E_Tests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false, null, "build", false)]
    [InlineData(false, false, "process", "build", false)]
    [InlineData(false, false, "process", null, false)]
    [InlineData(false, true, null, "build", false)]
    [InlineData(false, true, "process", "build", false)]
    [InlineData(false, true, "process", null, false)]
    [InlineData(true, false, null, "build", false)]
    [InlineData(true, false, "process", "build", false)]
    [InlineData(true, false, "process", null, false)]
    [InlineData(true, false, null, "build", true)]
    [InlineData(true, false, "process", "build", true)]
    [InlineData(true, false, "process", null, true)]
    public void TaskEnvironmentSurvivesTaskHostReconciliation(
        bool multiThreaded, bool explicitTaskHost, string? processValue, string? buildValue, bool disableChangeWave)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disableChangeWave ? "18.12" : null);
        string variable = $"MSBUILD_TASK_ENVIRONMENT_{Guid.NewGuid():N}";
        env.SetEnvironmentVariable(variable, processValue);
        string taskFactory = explicitTaskHost ? """TaskFactory="TaskHostFactory" """ : string.Empty;

        TransientTestFile project = env.CreateFile("environment.proj", $"""
            <Project>
              <UsingTask TaskName="ReadEnvironmentVariableTask" AssemblyFile="{typeof(ReadEnvironmentVariableTask).Assembly.Location}" {taskFactory}/>
              <UsingTask TaskName="SetEnvironmentVariableTask" AssemblyFile="{typeof(SetEnvironmentVariableTask).Assembly.Location}" {taskFactory}/>
              <Target Name="Build">
                <ReadEnvironmentVariableTask VariableName="{variable}">
                  <Output TaskParameter="Value" PropertyName="FirstValue" />
                  <Output TaskParameter="Pid" PropertyName="FirstPid" />
                </ReadEnvironmentVariableTask>
                <ReadEnvironmentVariableTask VariableName="{variable}">
                  <Output TaskParameter="Value" PropertyName="SecondValue" />
                  <Output TaskParameter="Pid" PropertyName="SecondPid" />
                </ReadEnvironmentVariableTask>
                <SetEnvironmentVariableTask VariableName="{variable}" Value="changed-by-task" />
                <ReadEnvironmentVariableTask VariableName="{variable}">
                  <Output TaskParameter="Value" PropertyName="ChangedValue" />
                  <Output TaskParameter="Pid" PropertyName="LastPid" />
                </ReadEnvironmentVariableTask>
              </Target>
            </Project>
            """);

        MockLogger logger = new(output);
        BuildParameters parameters = new()
        {
            MultiThreaded = multiThreaded,
            DisableInProcNode = false,
            EnableNodeReuse = false,
            Loggers = [logger],
        };
        parameters.SetBuildProcessEnvironmentVariable(variable, buildValue);

        using BuildManager manager = new();
        BuildResult result = manager.Build(parameters, new BuildRequestData(
            project.Path, new Dictionary<string, string?>(), null, ["Build"], null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild));

        result.ShouldHaveSucceeded();
        ProjectInstance? state = result.ProjectStateAfterBuild;
        state.ShouldNotBeNull();
        string expectedValue = (disableChangeWave ? processValue : buildValue) ?? string.Empty;
        state.GetPropertyValue("FirstValue").ShouldBe(expectedValue);
        state.GetPropertyValue("SecondValue").ShouldBe(expectedValue);
        state.GetPropertyValue("ChangedValue").ShouldBe("changed-by-task");
        string firstPid = state.GetPropertyValue("FirstPid");
        firstPid.ShouldBe(state.GetPropertyValue("SecondPid"));
        firstPid.ShouldBe(state.GetPropertyValue("LastPid"));
        using Process currentProcess = Process.GetCurrentProcess();
        string currentPid = currentProcess.Id.ToString(CultureInfo.InvariantCulture);
        if (multiThreaded || explicitTaskHost)
        {
            firstPid.ShouldNotBe(currentPid);
        }
        else
        {
            firstPid.ShouldBe(currentPid);
        }

        Environment.GetEnvironmentVariable(variable).ShouldBe(processValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("second-build")]
    public void ReusedTaskHostObservesTheNextBuildEnvironment(string? nextValue)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        string variable = $"MSBUILD_REUSED_TASK_ENVIRONMENT_{Guid.NewGuid():N}";
        env.SetEnvironmentVariable(variable, "process");

        TransientTestFile project = env.CreateFile("reuse.proj", $"""
            <Project>
              <UsingTask TaskName="ReadEnvironmentVariableTask" AssemblyFile="{typeof(ReadEnvironmentVariableTask).Assembly.Location}" />
              <Target Name="Build">
                <ReadEnvironmentVariableTask VariableName="{variable}">
                  <Output TaskParameter="Value" PropertyName="ObservedValue" />
                  <Output TaskParameter="Pid" PropertyName="TaskPid" />
                </ReadEnvironmentVariableTask>
              </Target>
            </Project>
            """);

        using BuildManager manager = new();
        ProjectInstance first = Build("first-build");
        ProjectInstance second = Build(nextValue);

        first.GetPropertyValue("ObservedValue").ShouldBe("first-build");
        second.GetPropertyValue("ObservedValue").ShouldBe(nextValue ?? string.Empty);
        second.GetPropertyValue("TaskPid").ShouldBe(first.GetPropertyValue("TaskPid"));
        Environment.GetEnvironmentVariable(variable).ShouldBe("process");

        ProjectInstance Build(string? value)
        {
            MockLogger logger = new(output);
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                DisableInProcNode = false,
                EnableNodeReuse = true,
                Loggers = [logger],
            };
            parameters.SetBuildProcessEnvironmentVariable(variable, value);
            BuildResult result = manager.Build(parameters, new BuildRequestData(
                project.Path, new Dictionary<string, string?>(), null, ["Build"], null,
                BuildRequestDataFlags.ProvideProjectStateAfterBuild));
            result.ShouldHaveSucceeded();
            ProjectInstance? state = result.ProjectStateAfterBuild;
            state.ShouldNotBeNull();
            int taskPid = int.Parse(state.GetPropertyValue("TaskPid"), CultureInfo.InvariantCulture);
            using Process currentProcess = Process.GetCurrentProcess();
            taskPid.ShouldNotBe(currentProcess.Id);
            env.WithTransientProcess(taskPid);
            return state;
        }
    }

    [Fact]
    public void ConcurrentBuildManagersKeepTaskEnvironmentsIsolated()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        string variable = $"MSBUILD_PARALLEL_TASK_ENVIRONMENT_{Guid.NewGuid():N}";
        env.SetEnvironmentVariable(variable, "process");
        TransientTestFile project = env.CreateFile("parallel.proj", $"""
            <Project>
              <UsingTask TaskName="ReadEnvironmentVariableTask" AssemblyFile="{typeof(ReadEnvironmentVariableTask).Assembly.Location}" />
              <Target Name="Build">
                <ReadEnvironmentVariableTask VariableName="{variable}">
                  <Output TaskParameter="Value" PropertyName="ObservedValue" />
                </ReadEnvironmentVariableTask>
              </Target>
            </Project>
            """);

        using BuildManager firstManager = new();
        using BuildManager secondManager = new();
        firstManager.BeginBuild(CreateParameters("first-build"));
        secondManager.BeginBuild(CreateParameters("second-build"));
        Environment.GetEnvironmentVariable(variable).ShouldBe("process");
        BuildRequestData request = new(
            project.Path, new Dictionary<string, string?>(), null, ["Build"], null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);
        BuildSubmission first = firstManager.PendBuildRequest(request);
        BuildSubmission second = secondManager.PendBuildRequest(request);
        first.ExecuteAsync(null, null);
        second.ExecuteAsync(null, null);
        firstManager.EndBuild();
        secondManager.EndBuild();

        BuildResult firstResult = first.BuildResult.ShouldNotBeNull();
        BuildResult secondResult = second.BuildResult.ShouldNotBeNull();
        firstResult.ShouldHaveSucceeded();
        secondResult.ShouldHaveSucceeded();
        firstResult.ProjectStateAfterBuild.ShouldNotBeNull().GetPropertyValue("ObservedValue").ShouldBe("first-build");
        secondResult.ProjectStateAfterBuild.ShouldNotBeNull().GetPropertyValue("ObservedValue").ShouldBe("second-build");
        Environment.GetEnvironmentVariable(variable).ShouldBe("process");

        BuildParameters CreateParameters(string value)
        {
            BuildParameters parameters = new()
            {
                MultiThreaded = true,
                DisableInProcNode = false,
                EnableNodeReuse = false,
                SaveOperatingEnvironment = false,
                Loggers = [new MockLogger(output)],
            };
            parameters.SetBuildProcessEnvironmentVariable(variable, value);
            return parameters;
        }
    }

    [WindowsFullFrameworkOnlyFact]
    public void CrossBitnessTaskHostPreservesArchitectureEnvironment()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        string? nativeProgramFiles = Environment.GetEnvironmentVariable("ProgramW6432");
        nativeProgramFiles.ShouldNotBeNullOrEmpty();
        TransientTestFile project = env.CreateFile("architecture.proj", """
            <Project>
              <UsingTask TaskName="Exec"
                         AssemblyFile="$([System.IO.Path]::Combine('$(MSBuildToolsPath)', 'Microsoft.Build.Tasks.Core.dll'))"
                         TaskFactory="TaskHostFactory" Runtime="CLR4" Architecture="x64" />
              <Target Name="Build">
                <Exec Command="echo HOST_ARCHITECTURE=%PROCESSOR_ARCHITECTURE% &amp; echo HOST_PROGRAMFILES=%ProgramFiles%" />
              </Target>
            </Project>
            """);

        string buildOutput = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{project.Path}\" /mt /nr:false /v:n", out bool success, outputHelper: output);
        success.ShouldBeTrue(buildOutput);
        buildOutput.ShouldContain("HOST_ARCHITECTURE=AMD64");
        buildOutput.ShouldContain($"HOST_PROGRAMFILES={nativeProgramFiles}");
    }
}
