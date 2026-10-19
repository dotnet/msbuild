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
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(null, "build", false)]
    [InlineData("process", "build", false)]
    [InlineData("process", null, false)]
    [InlineData(null, "build", true)]
    public void TaskEnvironmentSurvivesTaskHostReconciliation(
        string? processValue, string? buildValue, bool disableChangeWave)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disableChangeWave ? "18.13" : null);
        string variable = $"MSBUILD_TASK_ENVIRONMENT_{Guid.NewGuid():N}";
        env.SetEnvironmentVariable(variable, processValue);
        TransientTestFile project = env.CreateFile("environment.proj", $"""
            <Project>
              <UsingTask TaskName="ReadEnvironmentVariableTask" AssemblyFile="{typeof(ReadEnvironmentVariableTask).Assembly.Location}" />
              <UsingTask TaskName="SetEnvironmentVariableTask" AssemblyFile="{typeof(SetEnvironmentVariableTask).Assembly.Location}" />
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

        MockLogger logger = new(_output);
        BuildParameters parameters = new()
        {
            MultiThreaded = true,
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
        firstPid.ShouldNotBe(currentPid);

        Environment.GetEnvironmentVariable(variable).ShouldBe(processValue);
    }

    [Fact]
    public void ReusedTaskHostObservesTheNextBuildEnvironment()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
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
        ProjectInstance second = Build(null);

        first.GetPropertyValue("ObservedValue").ShouldBe("first-build");
        second.GetPropertyValue("ObservedValue").ShouldBeEmpty();
        second.GetPropertyValue("TaskPid").ShouldBe(first.GetPropertyValue("TaskPid"));
        Environment.GetEnvironmentVariable(variable).ShouldBe("process");

        ProjectInstance Build(string? value)
        {
            MockLogger logger = new(_output);
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
    public void ConcurrentProjectsKeepTaskEnvironmentsIsolated()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        string variable = $"MSBUILD_PARALLEL_TASK_ENVIRONMENT_{Guid.NewGuid():N}";
        env.SetEnvironmentVariable(variable, null);
        TransientTestFile project = env.CreateFile("parallel.proj", $"""
            <Project>
              <UsingTask TaskName="SetVirtualEnvironmentVariableTask" AssemblyFile="{typeof(SetVirtualEnvironmentVariableTask).Assembly.Location}" />
              <UsingTask TaskName="ReadEnvironmentVariableTask" AssemblyFile="{typeof(ReadEnvironmentVariableTask).Assembly.Location}" />
              <Target Name="Build">
                <SetVirtualEnvironmentVariableTask VariableName="{variable}" Value="$(BuildValue)">
                  <Output TaskParameter="Pid" PropertyName="SetterPid" />
                </SetVirtualEnvironmentVariableTask>
                <ReadEnvironmentVariableTask VariableName="{variable}">
                  <Output TaskParameter="Value" PropertyName="ObservedValue" />
                  <Output TaskParameter="Pid" PropertyName="ReaderPid" />
                </ReadEnvironmentVariableTask>
              </Target>
            </Project>
            """);

        MockLogger logger = new(_output);
        BuildParameters parameters = new()
        {
            MultiThreaded = true,
            MaxNodeCount = 2,
            DisableInProcNode = false,
            EnableNodeReuse = false,
            SaveOperatingEnvironment = false,
            Loggers = [logger],
        };
        using BuildManager manager = new();
        manager.BeginBuild(parameters);
        BuildSubmission first = manager.PendBuildRequest(CreateRequest("first-build"));
        BuildSubmission second = manager.PendBuildRequest(CreateRequest("second-build"));
        first.ExecuteAsync(null, null);
        second.ExecuteAsync(null, null);
        manager.EndBuild();

        BuildResult firstResult = first.BuildResult.ShouldNotBeNull();
        BuildResult secondResult = second.BuildResult.ShouldNotBeNull();
        firstResult.ShouldHaveSucceeded();
        secondResult.ShouldHaveSucceeded();
        AssertState(firstResult, "first-build");
        AssertState(secondResult, "second-build");
        Environment.GetEnvironmentVariable(variable).ShouldBeNull();

        BuildRequestData CreateRequest(string value) => new(
            project.Path, new Dictionary<string, string?> { ["BuildValue"] = value }, null, ["Build"], null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);

        void AssertState(BuildResult result, string value)
        {
            ProjectInstance state = result.ProjectStateAfterBuild.ShouldNotBeNull();
            state.GetPropertyValue("ObservedValue").ShouldBe(value);
            using Process currentProcess = Process.GetCurrentProcess();
            state.GetPropertyValue("SetterPid").ShouldBe(currentProcess.Id.ToString(CultureInfo.InvariantCulture));
            state.GetPropertyValue("ReaderPid").ShouldNotBe(state.GetPropertyValue("SetterPid"));
        }
    }

    [MSBuildMultiThreadableTask]
    public class SetVirtualEnvironmentVariableTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        [Required]
        public string VariableName { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        [Output]
        public int Pid { get; set; }

        public override bool Execute()
        {
            TaskEnvironment.SetEnvironmentVariable(VariableName, Value);
            using Process currentProcess = Process.GetCurrentProcess();
            Pid = currentProcess.Id;
            return true;
        }
    }

    [WindowsFullFrameworkOnlyFact]
    public void CrossBitnessTaskHostPreservesArchitectureEnvironment()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
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
            $"\"{project.Path}\" /mt /nr:false /v:n", out bool success, outputHelper: _output);
        success.ShouldBeTrue(buildOutput);
        buildOutput.ShouldContain("HOST_ARCHITECTURE=AMD64");
        buildOutput.ShouldContain($"HOST_PROGRAMFILES={nativeProgramFiles}");
    }
}
