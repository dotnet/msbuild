// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.TelemetryInfra;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests
{
    [Collection("OpenTelemetryManagerTests")]
    public class Telemetry_Tests
    {
        private readonly ITestOutputHelper _output;

        public Telemetry_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        private sealed class ProjectFinishedCapturingLogger : ILogger
        {
            private readonly List<ProjectFinishedEventArgs> _projectFinishedEventArgs = [];
            public LoggerVerbosity Verbosity { get; set; }
            public string? Parameters { get; set; }

            public IReadOnlyList<ProjectFinishedEventArgs> ProjectFinishedEventArgsReceived =>
                _projectFinishedEventArgs;

            public void Initialize(IEventSource eventSource)
            {
                eventSource.ProjectFinished += EventSource_ProjectFinished;
            }

            private void EventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
            {
                _projectFinishedEventArgs.Add(e);
            }

            public void Shutdown()
            { }
        }

        [Fact]
        public void WorkerNodeTelemetryCollection_BasicTarget()
        {
            WorkerNodeTelemetryData? workerNodeTelemetryData = null;
            InternalTelemetryConsumingLogger.TestOnly_InternalTelemetryAggregted += dt => workerNodeTelemetryData = dt;

            var testProject = """
                                      <Project>
                                          <Target Name="Build">
                                              <Message Text="Hello World"/>
                                              <CreateItem Include="foo.bar">
                                                  <Output TaskParameter="Include" ItemName="I" />
                                              </CreateItem>
                                              <Message Text="Bye World"/>
                                          </Target>
                                      </Project>
                              """;

            MockLogger logger = new MockLogger(_output);
            Helpers.BuildProjectContentUsingBuildManager(testProject, logger,
                new BuildParameters() { IsTelemetryEnabled = true }).OverallResult.ShouldBe(BuildResultCode.Success);

            workerNodeTelemetryData!.ShouldNotBeNull();
            var buildTargetKey = new TaskOrTargetTelemetryKey("Build", true, false);
            workerNodeTelemetryData.TargetsExecutionData.ShouldContainKey(buildTargetKey);
            workerNodeTelemetryData.TargetsExecutionData[buildTargetKey].ShouldBeTrue();
            workerNodeTelemetryData.TargetsExecutionData.Keys.Count.ShouldBe(1);

            workerNodeTelemetryData.TasksExecutionData.Keys.Count.ShouldBeGreaterThan(2);
            ((int)workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.Message"].ExecutionsCount).ShouldBe(2);
            workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.Message"].CumulativeExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);
            ((int)workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.CreateItem"].ExecutionsCount).ShouldBe(1);
            workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.CreateItem"].CumulativeExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);

            workerNodeTelemetryData.TasksExecutionData.Keys.ShouldAllBe(k => !k.IsCustom && !k.IsNuget);
            workerNodeTelemetryData.TasksExecutionData.Values
                .Count(v => v.CumulativeExecutionTime > TimeSpan.Zero || v.ExecutionsCount > 0).ShouldBe(2);
        }

        [Fact]
        public void WorkerNodeTelemetryCollection_CustomTargetsAndTasks()
        {
            WorkerNodeTelemetryData? workerNodeTelemetryData = null;
            InternalTelemetryConsumingLogger.TestOnly_InternalTelemetryAggregted += dt => workerNodeTelemetryData = dt;

            var testProject = """
                                      <Project>
                                      <UsingTask
                                          TaskName="Task01"
                                          TaskFactory="RoslynCodeTaskFactory"
                                          AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll" >
                                          <ParameterGroup />
                                          <Task>
                                            <Code Type="Fragment" Language="cs">
                                              Log.LogMessage(MessageImportance.Low, "Hello, world!");
                                            </Code>
                                          </Task>
                                       </UsingTask>

                                       <UsingTask
                                         TaskName="Task02"
                                         TaskFactory="RoslynCodeTaskFactory"
                                         AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll" >
                                         <ParameterGroup />
                                         <Task>
                                           <Code Type="Fragment" Language="cs">
                                             Log.LogMessage(MessageImportance.High, "Hello, world!");
                                           </Code>
                                         </Task>
                                      </UsingTask>

                                          <Target Name="Build" DependsOnTargets="BeforeBuild">
                                              <Message Text="Hello World"/>
                                              <CreateItem Include="foo.bar">
                                                  <Output TaskParameter="Include" ItemName="I" />
                                              </CreateItem>
                                              <Task01 />
                                              <Message Text="Bye World"/>
                                          </Target>

                                          <Target Name="BeforeBuild">
                                              <Message Text="Hello World"/>
                                              <Task01 />
                                          </Target>

                                          <Target Name="NotExecuted">
                                              <Message Text="Hello World"/>
                                          </Target>
                                      </Project>
                              """;
            MockLogger logger = new MockLogger(_output);
            Helpers.BuildProjectContentUsingBuildManager(testProject, logger,
                new BuildParameters() { IsTelemetryEnabled = true }).OverallResult.ShouldBe(BuildResultCode.Success);

            workerNodeTelemetryData!.ShouldNotBeNull();
            workerNodeTelemetryData.TargetsExecutionData.ShouldContainKey(new TaskOrTargetTelemetryKey("Build", true, false));
            workerNodeTelemetryData.TargetsExecutionData[new TaskOrTargetTelemetryKey("Build", true, false)].ShouldBeTrue();
            workerNodeTelemetryData.TargetsExecutionData.ShouldContainKey(new TaskOrTargetTelemetryKey("BeforeBuild", true, false));
            workerNodeTelemetryData.TargetsExecutionData[new TaskOrTargetTelemetryKey("BeforeBuild", true, false)].ShouldBeTrue();
            workerNodeTelemetryData.TargetsExecutionData.ShouldContainKey(new TaskOrTargetTelemetryKey("NotExecuted", true, false));
            workerNodeTelemetryData.TargetsExecutionData[new TaskOrTargetTelemetryKey("NotExecuted", true, false)].ShouldBeFalse();
            workerNodeTelemetryData.TargetsExecutionData.Keys.Count.ShouldBe(3);

            workerNodeTelemetryData.TasksExecutionData.Keys.Count.ShouldBeGreaterThan(2);
            ((int)workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.Message"].ExecutionsCount).ShouldBe(3);
            workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.Message"].CumulativeExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);
            ((int)workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.CreateItem"].ExecutionsCount).ShouldBe(1);
            workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.CreateItem"].CumulativeExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);

            ((int)workerNodeTelemetryData.TasksExecutionData[new TaskOrTargetTelemetryKey("Task01", true, false)].ExecutionsCount).ShouldBe(2);
            workerNodeTelemetryData.TasksExecutionData[new TaskOrTargetTelemetryKey("Task01", true, false)].CumulativeExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);

            ((int)workerNodeTelemetryData.TasksExecutionData[new TaskOrTargetTelemetryKey("Task02", true, false)].ExecutionsCount).ShouldBe(0);
            workerNodeTelemetryData.TasksExecutionData[new TaskOrTargetTelemetryKey("Task02", true, false)].CumulativeExecutionTime.ShouldBe(TimeSpan.Zero);

            workerNodeTelemetryData.TasksExecutionData.Values
                .Count(v => v.CumulativeExecutionTime > TimeSpan.Zero || v.ExecutionsCount > 0).ShouldBe(3);

            workerNodeTelemetryData.TasksExecutionData.Keys.ShouldAllBe(k => !k.IsNuget);
        }

#if NET
        // test in .net core with opentelemetry opted in to avoid sending it but enable listening to it
        [Fact]
        public void NodeTelemetryE2E()
        {
            using TestEnvironment env = TestEnvironment.Create();
            env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTIN", "1");
            env.SetEnvironmentVariable("MSBUILD_TELEMETRY_SAMPLE_RATE", "1.0");
            env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", null);
            env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", null);

            // Reset the OpenTelemetryManager state to ensure clean test
            var instance = OpenTelemetryManager.Instance;
            typeof(OpenTelemetryManager)
                .GetField("_telemetryState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(instance, OpenTelemetryManager.TelemetryState.Uninitialized);

            typeof(OpenTelemetryManager)
                .GetProperty("DefaultActivitySource",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(instance, null);

            // track activities through an ActivityListener
            var capturedActivities = new List<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.DefaultActivitySourceNamespace),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = capturedActivities.Add,
                ActivityStopped = _ => { }
            };
            ActivitySource.AddActivityListener(listener);

            var testProject = @"
            <Project>
                <Target Name='Build'>
                    <Message Text='Start'/>
                    <CreateItem Include='test.txt'>
                        <Output TaskParameter='Include' ItemName='TestItem' />
                    </CreateItem>
                    <Message Text='End'/>
                </Target>
                <Target Name='Clean'>
                    <Message Text='Cleaning...'/>
                </Target>
            </Project>";

            using var testEnv = TestEnvironment.Create(_output);
            var projectFile = testEnv.CreateFile("test.proj", testProject).Path;

            // Set up loggers
            var projectFinishedLogger = new ProjectFinishedCapturingLogger();
            var buildParameters = new BuildParameters
            {
                Loggers = new ILogger[] { projectFinishedLogger },
                IsTelemetryEnabled = true
            };

            // Act
            using (var buildManager = new BuildManager())
            {
                // Phase 1: Begin Build - This initializes telemetry infrastructure
                buildManager.BeginBuild(buildParameters);

                // Phase 2: Execute build requests
                var buildRequestData1 = new BuildRequestData(
                    projectFile,
                    new Dictionary<string, string?>(),
                    null,
                    new[] { "Build" },
                    null);

                buildManager.BuildRequest(buildRequestData1);

                var buildRequestData2 = new BuildRequestData(
                    projectFile,
                    new Dictionary<string, string?>(),
                    null,
                    new[] { "Clean" },
                    null);

                buildManager.BuildRequest(buildRequestData2);

                // Phase 3: End Build - This puts telemetry to an system.diagnostics activity
                buildManager.EndBuild();

                // Verify build activity were captured by the listener and contain task and target info
                capturedActivities.ShouldNotBeEmpty();
                var activity = capturedActivities.FindLast(a => a.DisplayName == "VS/MSBuild/Build").ShouldNotBeNull();
                var tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value);
                tags.ShouldNotBeNull();

                tags.ShouldContainKey("VS.MSBuild.BuildTarget");
                tags["VS.MSBuild.BuildTarget"].ShouldNotBeNullOrEmpty();

                // Verify task data
                tags.ShouldContainKey("VS.MSBuild.Tasks");
                var tasksJson = tags["VS.MSBuild.Tasks"];
                tasksJson.ShouldNotBeNullOrEmpty();
                tasksJson.ShouldContain("Microsoft.Build.Tasks.Message");
                tasksJson.ShouldContain("Microsoft.Build.Tasks.CreateItem");

                // Parse tasks data for detailed assertions
                var tasksData = JsonSerializer.Deserialize<JsonElement>(tasksJson);

                // Verify Message task execution metrics - updated for object structure
                tasksData.TryGetProperty("Microsoft.Build.Tasks.Message", out var messageTask).ShouldBe(true);
                messageTask.GetProperty("ExecutionsCount").GetInt32().ShouldBe(3);
                messageTask.GetProperty("TotalMilliseconds").GetDouble().ShouldBeGreaterThan(0);
                messageTask.GetProperty("TotalMemoryBytes").GetInt64().ShouldBeGreaterThan(0);
                messageTask.GetProperty(nameof(TaskOrTargetTelemetryKey.IsCustom)).GetBoolean().ShouldBe(false);
                messageTask.GetProperty(nameof(TaskOrTargetTelemetryKey.IsCustom)).GetBoolean().ShouldBe(false);

                // Verify CreateItem task execution metrics - updated for object structure
                tasksData.TryGetProperty("Microsoft.Build.Tasks.CreateItem", out var createItemTask).ShouldBe(true);
                createItemTask.GetProperty("ExecutionsCount").GetInt32().ShouldBe(1);
                createItemTask.GetProperty("TotalMilliseconds").GetDouble().ShouldBeGreaterThan(0);
                createItemTask.GetProperty("TotalMemoryBytes").GetInt64().ShouldBeGreaterThan(0);

                // Verify Targets summary information
                tags.ShouldContainKey("VS.MSBuild.TargetsSummary");
                var targetsSummaryJson = tags["VS.MSBuild.TargetsSummary"];
                targetsSummaryJson.ShouldNotBeNullOrEmpty();
                var targetsSummary = JsonSerializer.Deserialize<JsonElement>(targetsSummaryJson);

                // Verify loaded and executed targets counts - match structure in TargetsSummaryConverter.Write
                targetsSummary.GetProperty("Loaded").GetProperty("Total").GetInt32().ShouldBe(2);
                targetsSummary.GetProperty("Executed").GetProperty("Total").GetInt32().ShouldBe(2);

                // Verify Tasks summary information
                tags.ShouldContainKey("VS.MSBuild.TasksSummary");
                var tasksSummaryJson = tags["VS.MSBuild.TasksSummary"];
                tasksSummaryJson.ShouldNotBeNullOrEmpty();
                var tasksSummary = JsonSerializer.Deserialize<JsonElement>(tasksSummaryJson);

                // Verify task execution summary metrics based on TasksSummaryConverter.Write structure
                tasksSummary.GetProperty("Microsoft").GetProperty("Total").GetProperty("ExecutionsCount").GetInt32().ShouldBe(4);
                tasksSummary.GetProperty("Microsoft").GetProperty("Total").GetProperty("TotalMilliseconds").GetDouble().ShouldBeGreaterThan(0);
                tasksSummary.GetProperty("Microsoft").GetProperty("Total").GetProperty("TotalMemoryBytes").GetInt64().ShouldBeGreaterThan(0);
            }
        }

#endif
    }
}
