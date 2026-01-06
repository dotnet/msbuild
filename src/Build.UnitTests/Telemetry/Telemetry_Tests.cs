// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.TelemetryInfra;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Build.Framework.Telemetry.BuildInsights;
using static Microsoft.Build.Framework.Telemetry.TelemetryDataUtils;

namespace Microsoft.Build.Engine.UnitTests
{
    [Collection("TelemetryManagerTests")]
    public class Telemetry_Tests
    {
        private readonly ITestOutputHelper _output;

        public Telemetry_Tests(ITestOutputHelper output)
        {
            _output = output;
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
            workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.Message"].ExecutionsCount.ShouldBe(2);
            workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.Message"].CumulativeExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);
            workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.CreateItem"].ExecutionsCount.ShouldBe(1);
            workerNodeTelemetryData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.CreateItem"].CumulativeExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);

            workerNodeTelemetryData.TasksExecutionData.Keys.ShouldAllBe(k => !k.IsCustom && !k.IsNuget);
            workerNodeTelemetryData.TasksExecutionData.Values
                .Count(v => v.CumulativeExecutionTime > TimeSpan.Zero || v.ExecutionsCount > 0).ShouldBe(2);
        }

        [Fact]
        public void WorkerNodeTelemetryCollection_CustomTargetsAndTasks()
        {
            WorkerNodeTelemetryData? workerNodeData = null;
            InternalTelemetryConsumingLogger.TestOnly_InternalTelemetryAggregted += dt => workerNodeData = dt;

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
            Helpers.BuildProjectContentUsingBuildManager(
                testProject,
                logger,
                new BuildParameters() { IsTelemetryEnabled = true }).OverallResult.ShouldBe(BuildResultCode.Success);

            workerNodeData!.ShouldNotBeNull();
            workerNodeData.TargetsExecutionData.ShouldContainKey(new TaskOrTargetTelemetryKey("Build", true, false));
            workerNodeData.TargetsExecutionData[new TaskOrTargetTelemetryKey("Build", true, false)].ShouldBeTrue();
            workerNodeData.TargetsExecutionData.ShouldContainKey(new TaskOrTargetTelemetryKey("BeforeBuild", true, false));
            workerNodeData.TargetsExecutionData[new TaskOrTargetTelemetryKey("BeforeBuild", true, false)].ShouldBeTrue();
            workerNodeData.TargetsExecutionData.ShouldContainKey(new TaskOrTargetTelemetryKey("NotExecuted", true, false));
            workerNodeData.TargetsExecutionData[new TaskOrTargetTelemetryKey("NotExecuted", true, false)].ShouldBeFalse();
            workerNodeData.TargetsExecutionData.Keys.Count.ShouldBe(3);

            workerNodeData.TasksExecutionData.Keys.Count.ShouldBeGreaterThan(2);
            workerNodeData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.Message"].ExecutionsCount.ShouldBe(3);
            workerNodeData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.Message"].CumulativeExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);
            workerNodeData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.CreateItem"].ExecutionsCount.ShouldBe(1);
            workerNodeData.TasksExecutionData[(TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.CreateItem"].CumulativeExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);

            workerNodeData.TasksExecutionData[new TaskOrTargetTelemetryKey("Task01", true, false)].ExecutionsCount.ShouldBe(2);
            workerNodeData.TasksExecutionData[new TaskOrTargetTelemetryKey("Task01", true, false)].CumulativeExecutionTime.ShouldBeGreaterThan(TimeSpan.Zero);

            workerNodeData.TasksExecutionData[new TaskOrTargetTelemetryKey("Task02", true, false)].ExecutionsCount.ShouldBe(0);
            workerNodeData.TasksExecutionData[new TaskOrTargetTelemetryKey("Task02", true, false)].CumulativeExecutionTime.ShouldBe(TimeSpan.Zero);

            workerNodeData.TasksExecutionData.Values.Count(v => v.CumulativeExecutionTime > TimeSpan.Zero || v.ExecutionsCount > 0).ShouldBe(3);

            workerNodeData.TasksExecutionData.Keys.ShouldAllBe(k => !k.IsNuget);
        }

#if NET
        // test in .net core with telemetry opted in to avoid sending it but enable listening to it
        [Fact]
        public void NodeTelemetryE2E()
        {
            using TestEnvironment env = TestEnvironment.Create();
            env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", null);
            env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", null);

            var capturedActivities = new List<Activity>();
            using var activityStoppedEvent = new ManualResetEventSlim(false);
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.DefaultActivitySourceNamespace),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = a => { lock (capturedActivities) { capturedActivities.Add(a); } },
                ActivityStopped = a =>
                {
                    if (a.DisplayName == "VS/MSBuild/Build")
                    {
                        activityStoppedEvent.Set();
                    }
                },
            };
            ActivitySource.AddActivityListener(listener);

            // Reset TelemetryManager to force re-initialization with our listener active
            TelemetryManager.ResetForTest();

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
            }

            // Wait for the activity to be fully processed
            activityStoppedEvent.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue("Timed out waiting for build activity to stop");

            // Verify build activity were captured by the listener and contain task and target info
            capturedActivities.ShouldNotBeEmpty();
            var activity = capturedActivities.FindLast(a => a.DisplayName == "VS/MSBuild/Build").ShouldNotBeNull();
            var tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value);
            tags.ShouldNotBeNull();

            tags.ShouldContainKey("VS.MSBuild.BuildTarget");
            tags["VS.MSBuild.BuildTarget"].ShouldNotBeNullOrEmpty();

            // Verify task data
            var tasks = activity.TagObjects.FirstOrDefault(to => to.Key == "VS.MSBuild.Tasks");

            var tasksData = tasks.Value as List<TaskDetailInfo>;
            var messageTaskData = tasksData!.FirstOrDefault(t => t.Name == "Microsoft.Build.Tasks.Message");
            messageTaskData.ShouldNotBeNull();

            // Verify Message task execution metrics 
            messageTaskData.ExecutionsCount.ShouldBe(3);
            messageTaskData.TotalMilliseconds.ShouldBeGreaterThan(0);
            messageTaskData.TotalMemoryBytes.ShouldBeGreaterThanOrEqualTo(0);
            messageTaskData.IsCustom.ShouldBe(false);

            // Verify CreateItem task execution metrics
            var createItemTaskData = tasksData!.FirstOrDefault(t => t.Name == "Microsoft.Build.Tasks.CreateItem");
            createItemTaskData.ShouldNotBeNull();
            createItemTaskData.ExecutionsCount.ShouldBe(1);
            createItemTaskData.TotalMilliseconds.ShouldBeGreaterThan(0);
            createItemTaskData.TotalMemoryBytes.ShouldBeGreaterThanOrEqualTo(0);

            // Verify Targets summary information
            var targetsSummaryTagObject = activity.TagObjects.FirstOrDefault(to => to.Key.Contains("VS.MSBuild.TargetsSummary"));
            var targetsSummary = targetsSummaryTagObject.Value as TargetsSummaryInfo;
            targetsSummary.ShouldNotBeNull();
            targetsSummary.Loaded.Total.ShouldBe(2);
            targetsSummary.Executed.Total.ShouldBe(2);

            // Verify Tasks summary information
            var tasksSummaryTagObject = activity.TagObjects.FirstOrDefault(to => to.Key.Contains("VS.MSBuild.TasksSummary"));
            var tasksSummary = tasksSummaryTagObject.Value as TasksSummaryInfo;
            tasksSummary.ShouldNotBeNull();

            tasksSummary.Microsoft.ShouldNotBeNull();
            tasksSummary.Microsoft!.Total!.ExecutionsCount.ShouldBe(4);
            tasksSummary.Microsoft!.Total!.TotalMilliseconds.ShouldBeGreaterThan(0);

            // Allowing 0 for TotalMemoryBytes as it is possible for tasks to allocate no memory in certain scenarios.
            tasksSummary.Microsoft.Total.TotalMemoryBytes.ShouldBeGreaterThanOrEqualTo(0);
        }
#endif

        private sealed class ProjectFinishedCapturingLogger : ILogger
        {
            private readonly List<ProjectFinishedEventArgs> _projectFinishedEventArgs = [];

            public LoggerVerbosity Verbosity { get; set; }

            public string? Parameters { get; set; }

            public IReadOnlyList<ProjectFinishedEventArgs> ProjectFinishedEventArgsReceived => _projectFinishedEventArgs;

            public void Initialize(IEventSource eventSource) => eventSource.ProjectFinished += EventSource_ProjectFinished;

            private void EventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e) => _projectFinishedEventArgs.Add(e);

            public void Shutdown() { }
        }
    }
}
