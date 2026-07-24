// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.TelemetryInfra;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;
using static Microsoft.Build.Framework.Telemetry.BuildInsights;
using static Microsoft.Build.Framework.Telemetry.TelemetryDataUtils;
using static Microsoft.Build.TelemetryInfra.TasksDetailsTelemetry;

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
            workerNodeTelemetryData.TargetsExecutionData[buildTargetKey].WasExecuted.ShouldBeTrue();
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
            workerNodeData.TargetsExecutionData[new TaskOrTargetTelemetryKey("Build", true, false)].WasExecuted.ShouldBeTrue();
            workerNodeData.TargetsExecutionData.ShouldContainKey(new TaskOrTargetTelemetryKey("BeforeBuild", true, false));
            workerNodeData.TargetsExecutionData[new TaskOrTargetTelemetryKey("BeforeBuild", true, false)].WasExecuted.ShouldBeTrue();
            workerNodeData.TargetsExecutionData.ShouldContainKey(new TaskOrTargetTelemetryKey("NotExecuted", true, false));
            workerNodeData.TargetsExecutionData[new TaskOrTargetTelemetryKey("NotExecuted", true, false)].WasExecuted.ShouldBeFalse();
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

        [Fact]
        public void WorkerNodeTelemetryCollection_TaskFactoryName()
        {
            WorkerNodeTelemetryData? workerNodeData = null;
            InternalTelemetryConsumingLogger.TestOnly_InternalTelemetryAggregted += dt => workerNodeData = dt;

            var testProject = """
                              <Project>
                              <UsingTask
                                  TaskName="InlineTask01"
                                  TaskFactory="RoslynCodeTaskFactory"
                                  AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll" >
                                  <ParameterGroup />
                                  <Task>
                                    <Code Type="Fragment" Language="cs">
                                      Log.LogMessage(MessageImportance.Low, "Hello from inline task!");
                                    </Code>
                                  </Task>
                               </UsingTask>
                                  <Target Name="Build">
                                      <Message Text="Hello World"/>
                                      <InlineTask01 />
                                  </Target>
                              </Project>
                      """;

            MockLogger logger = new MockLogger(_output);
            Helpers.BuildProjectContentUsingBuildManager(
                testProject,
                logger,
                new BuildParameters() { IsTelemetryEnabled = true }).OverallResult.ShouldBe(BuildResultCode.Success);

            workerNodeData!.ShouldNotBeNull();

            // Verify built-in task has AssemblyTaskFactory
            var messageTaskKey = (TaskOrTargetTelemetryKey)"Microsoft.Build.Tasks.Message";
            workerNodeData.TasksExecutionData.ShouldContainKey(messageTaskKey);
            workerNodeData.TasksExecutionData[messageTaskKey].TaskFactoryName.ShouldBe("AssemblyTaskFactory");

            // Verify inline task has RoslynCodeTaskFactory
            var inlineTaskKey = new TaskOrTargetTelemetryKey("InlineTask01", true, false);
            workerNodeData.TasksExecutionData.ShouldContainKey(inlineTaskKey);
            workerNodeData.TasksExecutionData[inlineTaskKey].TaskFactoryName.ShouldBe("RoslynCodeTaskFactory");
            workerNodeData.TasksExecutionData[inlineTaskKey].ExecutionsCount.ShouldBe(1);
        }

        [Fact]
        public void TelemetryDataUtils_HashesCustomFactoryName()
        {
            // Create telemetry data with a custom factory name
            var tasksData = new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>
            {
                { new TaskOrTargetTelemetryKey("CustomTask", true, false), new TaskExecutionStats(TimeSpan.FromMilliseconds(100), 1, 1000, "MyCompany.CustomTaskFactory", null) },
                { new TaskOrTargetTelemetryKey("BuiltInTask", false, false), new TaskExecutionStats(TimeSpan.FromMilliseconds(50), 2, 500, "AssemblyTaskFactory", null) },
                { new TaskOrTargetTelemetryKey("InlineTask", true, false), new TaskExecutionStats(TimeSpan.FromMilliseconds(75), 1, 750, "RoslynCodeTaskFactory", "CLR4") }
            };
            var targetsData = new Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats>();
            var telemetryData = new WorkerNodeTelemetryData(tasksData, targetsData);

            var activityData = telemetryData.AsActivityDataHolder(includeTasksDetails: true, includeTargetDetails: false);
            activityData.ShouldNotBeNull();

            var properties = activityData.GetActivityProperties();
            properties.ShouldContainKey("Tasks");

            var taskDetails = properties["Tasks"] as List<TaskDetailInfo>;
            taskDetails.ShouldNotBeNull();

            // Custom factory name should be hashed
            var customTask = taskDetails!.FirstOrDefault(t => t.IsCustom && t.Name != GetHashed("InlineTask"));
            customTask.ShouldNotBeNull();
            customTask!.FactoryName.ShouldBe(GetHashed("MyCompany.CustomTaskFactory"));

            // Known factory names should NOT be hashed
            var builtInTask = taskDetails.FirstOrDefault(t => !t.IsCustom);
            builtInTask.ShouldNotBeNull();
            builtInTask!.FactoryName.ShouldBe("AssemblyTaskFactory");

            var inlineTask = taskDetails.FirstOrDefault(t => t.FactoryName == "RoslynCodeTaskFactory");
            inlineTask.ShouldNotBeNull();
            inlineTask!.FactoryName.ShouldBe("RoslynCodeTaskFactory");
            inlineTask.TaskHostRuntime.ShouldBe("CLR4");
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
                ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.DefaultActivitySourceNamespace, StringComparison.Ordinal),
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

            // Verify TaskFactoryName is populated for built-in tasks
            messageTaskData.FactoryName.ShouldBe("AssemblyTaskFactory");
            createItemTaskData.FactoryName.ShouldBe("AssemblyTaskFactory");

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

        [Fact]
        public void BuildIncrementalityInfo_AllTargetsExecuted_ClassifiedAsFull()
        {
            // Arrange: All targets were executed (none skipped)
            var targetsData = new Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats>
            {
                { new TaskOrTargetTelemetryKey("Build", false, false), TargetExecutionStats.Executed() },
                { new TaskOrTargetTelemetryKey("Compile", false, false), TargetExecutionStats.Executed() },
                { new TaskOrTargetTelemetryKey("Link", false, false), TargetExecutionStats.Executed() },
                { new TaskOrTargetTelemetryKey("Pack", false, false), TargetExecutionStats.Executed() },
            };
            var tasksData = new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>();
            var telemetryData = new WorkerNodeTelemetryData(tasksData, targetsData);

            // Act
            var activityData = telemetryData.AsActivityDataHolder(includeTasksDetails: false, includeTargetDetails: false);
            var properties = activityData!.GetActivityProperties();

            // Assert
            properties.ShouldContainKey("Incrementality");
            var incrementality = properties["Incrementality"] as BuildInsights.BuildIncrementalityInfo;
            incrementality.ShouldNotBeNull();
            incrementality!.Classification.ShouldBe(BuildInsights.BuildType.Full);
            incrementality.TotalTargetsCount.ShouldBe(4);
            incrementality.ExecutedTargetsCount.ShouldBe(4);
            incrementality.SkippedTargetsCount.ShouldBe(0);
            incrementality.IncrementalityRatio.ShouldBe(0.0);
        }

        [Fact]
        public void BuildIncrementalityInfo_MostTargetsSkipped_ClassifiedAsIncremental()
        {
            // Arrange: Most targets were skipped (>70%)
            var targetsData = new Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats>
            {
                { new TaskOrTargetTelemetryKey("Build", false, false), TargetExecutionStats.Skipped(TargetSkipReason.OutputsUpToDate) },
                { new TaskOrTargetTelemetryKey("Compile", false, false), TargetExecutionStats.Skipped(TargetSkipReason.OutputsUpToDate) },
                { new TaskOrTargetTelemetryKey("Link", false, false), TargetExecutionStats.Skipped(TargetSkipReason.ConditionWasFalse) },
                { new TaskOrTargetTelemetryKey("Pack", false, false), TargetExecutionStats.Executed() }, // Only one executed
            };
            var tasksData = new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>();
            var telemetryData = new WorkerNodeTelemetryData(tasksData, targetsData);

            // Act
            var activityData = telemetryData.AsActivityDataHolder(includeTasksDetails: false, includeTargetDetails: false);
            var properties = activityData!.GetActivityProperties();

            // Assert
            properties.ShouldContainKey("Incrementality");
            var incrementality = properties["Incrementality"] as BuildInsights.BuildIncrementalityInfo;
            incrementality.ShouldNotBeNull();
            incrementality!.Classification.ShouldBe(BuildInsights.BuildType.Incremental);
            incrementality.TotalTargetsCount.ShouldBe(4);
            incrementality.ExecutedTargetsCount.ShouldBe(1);
            incrementality.SkippedTargetsCount.ShouldBe(3);
            incrementality.SkippedDueToUpToDateCount.ShouldBe(2);
            incrementality.SkippedDueToConditionCount.ShouldBe(1);
            incrementality.SkippedDueToPreviouslyBuiltCount.ShouldBe(0);
            incrementality.IncrementalityRatio.ShouldBe(0.75);
        }

        [Fact]
        public void BuildIncrementalityInfo_NoTargets_ClassifiedAsUnknown()
        {
            // Arrange: No targets
            var targetsData = new Dictionary<TaskOrTargetTelemetryKey, TargetExecutionStats>();
            var tasksData = new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>();
            var telemetryData = new WorkerNodeTelemetryData(tasksData, targetsData);

            // Act
            var activityData = telemetryData.AsActivityDataHolder(includeTasksDetails: false, includeTargetDetails: false);
            var properties = activityData!.GetActivityProperties();

            // Assert
            properties.ShouldContainKey("Incrementality");
            var incrementality = properties["Incrementality"] as BuildInsights.BuildIncrementalityInfo;
            incrementality.ShouldNotBeNull();
            incrementality!.Classification.ShouldBe(BuildInsights.BuildType.Unknown);
            incrementality.TotalTargetsCount.ShouldBe(0);
            incrementality.IncrementalityRatio.ShouldBe(0.0);
        }

        [Fact]
        public void IsEmpty_TrueForDefault_FalseAfterAdd()
        {
            var data = new WorkerNodeTelemetryData();
            data.IsEmpty.ShouldBeTrue();

            var targetKey = new TaskOrTargetTelemetryKey("Target1", isCustom: false, isFromNugetCache: false, isFromMetaProject: false);
            data.AddTarget(targetKey, wasExecuted: true);
            data.IsEmpty.ShouldBeFalse();
        }

        [Fact]
        public void TelemetryCollector_AccumulatesAndSendsOnFinalize()
        {
            var collector = new TelemetryCollectorProvider.TelemetryCollector();
            var loggingService = new EventRecordingLoggingService();

            var loggingContext = new MockLoggingContext(
                loggingService,
                new BuildEventContext(1, 2, BuildEventContext.InvalidProjectContextId, 4));

            // Add data via the collector API.
            var key = new TaskOrTargetTelemetryKey("TestTarget", isCustom: true, isFromNugetCache: false, isFromMetaProject: false);
            collector.AddTarget(key, wasExecuted: true);

            // First FinalizeProcessing should emit a telemetry event.
            collector.FinalizeProcessing(loggingContext);
            var telemetryEvents = loggingService.RecordedEvents.OfType<WorkerNodeTelemetryEventArgs>().ToList();
            telemetryEvents.Count.ShouldBe(1);
            telemetryEvents[0].WorkerNodeTelemetryData.TargetsExecutionData.ShouldContainKey(key);

            // Second FinalizeProcessing on an empty collector should be a no-op (state was reset).
            collector.FinalizeProcessing(loggingContext);
            loggingService.RecordedEvents.OfType<WorkerNodeTelemetryEventArgs>().Count().ShouldBe(1, "No new event should be emitted after reset");

            // Add new data after reset - collector should still work.
            var key2 = new TaskOrTargetTelemetryKey("TestTarget2", isCustom: false, isFromNugetCache: false, isFromMetaProject: false);
            collector.AddTarget(key2, wasExecuted: false, skipReason: TargetSkipReason.ConditionWasFalse);

            // Third FinalizeProcessing should emit only the new data.
            collector.FinalizeProcessing(loggingContext);
            var allTelemetryEvents = loggingService.RecordedEvents.OfType<WorkerNodeTelemetryEventArgs>().ToList();
            allTelemetryEvents.Count.ShouldBe(2);
            allTelemetryEvents[1].WorkerNodeTelemetryData.TargetsExecutionData.ShouldContainKey(key2);
            allTelemetryEvents[1].WorkerNodeTelemetryData.TargetsExecutionData.ShouldNotContainKey(key, "Old data should not appear after reset");
        }

        [Fact]
        public void GetTasksDetailsProperties_ReturnsNullForNullData()
        {
            IWorkerNodeTelemetryData? data = null;
            data.GetTasksDetailsProperties().ShouldBeNull();
        }

        [Fact]
        public void GetTasksDetailsProperties_ReturnsNullForEmptyData()
        {
            var data = new WorkerNodeTelemetryData([], []);
            data.GetTasksDetailsProperties().ShouldBeNull();
        }

        /// <summary>
        /// Standard three-task fixture used by the <c>GetTasksDetailsProperties_*</c> tests:
        /// Copy (10 executions), Csc (5 executions), and a custom task (3 executions).
        /// </summary>
        private static Dictionary<string, string> BuildThreeTaskFixtureProperties()
        {
            var tasksData = new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>
            {
                { new TaskOrTargetTelemetryKey("Microsoft.Build.Tasks.Copy", false, false), new TaskExecutionStats(TimeSpan.FromMilliseconds(500), 10, 2048, "AssemblyTaskFactory", null) },
                { new TaskOrTargetTelemetryKey("Microsoft.Build.Tasks.Csc", false, false), new TaskExecutionStats(TimeSpan.FromMilliseconds(3000), 5, 4096, "AssemblyTaskFactory", null) },
                { new TaskOrTargetTelemetryKey("MyCustomTask", true, false), new TaskExecutionStats(TimeSpan.FromMilliseconds(100), 3, 512, "MyCompany.Factory", null) },
            };
            var data = new WorkerNodeTelemetryData(tasksData, []);
            Dictionary<string, string>? properties = data.GetTasksDetailsProperties();
            properties.ShouldNotBeNull();
            return properties!;
        }

        [Fact]
        public void GetTasksDetailsProperties_ReportsTaskCounts()
        {
            Dictionary<string, string> properties = BuildThreeTaskFixtureProperties();

            properties["TaskCount"].ShouldBe("3");
            properties["TotalTaskCount"].ShouldBe("3");
        }

        [Fact]
        public void GetTasksDetailsProperties_OrdersTasksByExecutionsCountDescending()
        {
            Dictionary<string, string> properties = BuildThreeTaskFixtureProperties();

            using var doc = System.Text.Json.JsonDocument.Parse(properties["Tasks"]);
            var tasks = doc.RootElement;
            tasks.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Array);
            tasks.GetArrayLength().ShouldBe(3);

            tasks[0].GetProperty("Name").GetString().ShouldBe("Microsoft.Build.Tasks.Copy");
            tasks[0].GetProperty("ExecutionsCount").GetInt32().ShouldBe(10);

            tasks[1].GetProperty("Name").GetString().ShouldBe("Microsoft.Build.Tasks.Csc");
            tasks[1].GetProperty("ExecutionsCount").GetInt32().ShouldBe(5);

            tasks[2].GetProperty("ExecutionsCount").GetInt32().ShouldBe(3);
        }

        [Fact]
        public void GetTasksDetailsProperties_HashesCustomTaskAndFactoryNames()
        {
            Dictionary<string, string> properties = BuildThreeTaskFixtureProperties();

            using var doc = System.Text.Json.JsonDocument.Parse(properties["Tasks"]);
            // Custom task is sorted last (lowest ExecutionsCount).
            var customTask = doc.RootElement[2];

            customTask.GetProperty("IsCustom").GetBoolean().ShouldBeTrue();
            customTask.GetProperty("Name").GetString().ShouldBe(GetHashed("MyCustomTask"));
            customTask.GetProperty("FactoryName").GetString().ShouldBe(GetHashed("MyCompany.Factory"));
        }

        [Fact]
        public void GetTasksDetailsProperties_BoundsToTop100()
        {
            var tasksData = new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>();
            for (int i = 0; i < 150; i++)
            {
                tasksData[new TaskOrTargetTelemetryKey($"Task{i}", false, false)] =
                    new TaskExecutionStats(TimeSpan.FromMilliseconds(i), i + 1, 0, "AssemblyTaskFactory", null);
            }

            var data = new WorkerNodeTelemetryData(tasksData, []);
            Dictionary<string, string>? properties = data.GetTasksDetailsProperties();

            properties.ShouldNotBeNull();
            properties!["TaskCount"].ShouldBe("100");
            properties["TotalTaskCount"].ShouldBe("150");

            // Parse the JSON and verify array length
            using var doc = System.Text.Json.JsonDocument.Parse(properties["Tasks"]);
            doc.RootElement.GetArrayLength().ShouldBe(100);

            // The top task by execution count should be Task149 (count=150)
            var first = doc.RootElement[0];
            first.GetProperty("Name").GetString().ShouldBe("Task149");
            first.GetProperty("ExecutionsCount").GetInt32().ShouldBe(150);
        }

        [Fact]
        public void GetTasksDetailsProperties_IncludesTaskHostRuntimeWhenNonNull()
        {
            var tasksData = new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>
            {
                { new TaskOrTargetTelemetryKey("Microsoft.Build.Tasks.Copy", false, false), new TaskExecutionStats(TimeSpan.FromMilliseconds(100), 5, 1024, "AssemblyTaskFactory", "CLR4") },
            };
            var data = new WorkerNodeTelemetryData(tasksData, []);

            Dictionary<string, string>? properties = data.GetTasksDetailsProperties();

            properties.ShouldNotBeNull();
            using var doc = System.Text.Json.JsonDocument.Parse(properties!["Tasks"]);
            var task = doc.RootElement[0];
            task.GetProperty("TaskHostRuntime").GetString().ShouldBe("CLR4");
        }

        [Fact]
        public void GetTasksDetailsProperties_ProducesValidJson()
        {
            var tasksData = new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>
            {
                { new TaskOrTargetTelemetryKey("Microsoft.Build.Tasks.Copy", false, false), new TaskExecutionStats(TimeSpan.FromMilliseconds(500), 10, 2048, "AssemblyTaskFactory", null) },
                { new TaskOrTargetTelemetryKey("MyCustomTask", true, true), new TaskExecutionStats(TimeSpan.FromMilliseconds(100), 3, 512, null, null) },
            };
            var data = new WorkerNodeTelemetryData(tasksData, []);

            Dictionary<string, string>? properties = data.GetTasksDetailsProperties();

            properties.ShouldNotBeNull();
            string json = properties!["Tasks"];

            // Should not throw - valid JSON
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            doc.RootElement.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Array);

            // Verify all expected properties are present
            var first = doc.RootElement[0];
            first.GetProperty("Name").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.String);
            first.GetProperty("ExecutionsCount").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Number);
            first.GetProperty("TotalMilliseconds").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Number);
            first.GetProperty("TotalMemoryBytes").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Number);
            first.GetProperty("IsCustom").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.False);
            first.GetProperty("IsNuget").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.False);
        }

        [Fact]
        public void GetTasksDetailsProperties_OmitsNullFactoryNameAndTaskHostRuntime()
        {
            // Both FactoryName and TaskHostRuntime are null - they should be omitted from JSON,
            // not emitted as null. This locks in JsonIgnoreCondition.WhenWritingNull behavior.
            var tasksData = new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>
            {
                { new TaskOrTargetTelemetryKey("Microsoft.Build.Tasks.Copy", false, false), new TaskExecutionStats(TimeSpan.FromMilliseconds(500), 10, 2048, null, null) },
            };
            var data = new WorkerNodeTelemetryData(tasksData, []);

            Dictionary<string, string>? properties = data.GetTasksDetailsProperties();

            properties.ShouldNotBeNull();
            using var doc = System.Text.Json.JsonDocument.Parse(properties!["Tasks"]);
            var task = doc.RootElement[0];

            task.TryGetProperty("FactoryName", out _).ShouldBeFalse();
            task.TryGetProperty("TaskHostRuntime", out _).ShouldBeFalse();
        }

        [Fact]
        public void GetTasksDetailsProperties_EscapesSpecialCharactersInFactoryName()
        {
            // Custom factory names are hashed (so they don't contain special chars in practice),
            // but a built-in factory name with embedded quotes or backslashes must still round-trip
            // through JSON without producing invalid output.
            var tasksData = new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>
            {
                { new TaskOrTargetTelemetryKey("Microsoft.Build.Tasks.Copy", false, false), new TaskExecutionStats(TimeSpan.FromMilliseconds(1), 1, 0, "Factory\"with\\special\nchars", null) },
            };
            var data = new WorkerNodeTelemetryData(tasksData, []);

            Dictionary<string, string>? properties = data.GetTasksDetailsProperties();

            properties.ShouldNotBeNull();

            // The hashed factory-name pipeline only hashes non-known factories, so this value gets hashed.
            // What we care about here: the resulting JSON must parse cleanly regardless of input.
            using var doc = System.Text.Json.JsonDocument.Parse(properties!["Tasks"]);
            doc.RootElement.GetArrayLength().ShouldBe(1);
        }

        [Fact]
        public void GetTasksDetailsProperties_EmitsNumericAndBooleanFieldsAsJsonPrimitives()
        {
            // Guards against regressions where numbers/bools could be accidentally serialized as strings
            // (e.g. via a custom JsonConverter). The SDK consumer parses these as numbers in Kusto.
            var tasksData = new Dictionary<TaskOrTargetTelemetryKey, TaskExecutionStats>
            {
                // Use ticks (1 ms = 10_000 ticks) so the fractional millisecond value round-trips on both
                // net472 (where TimeSpan.FromMilliseconds(double) rounds to whole ms) and net10.
                { new TaskOrTargetTelemetryKey("Microsoft.Build.Tasks.Copy", false, true), new TaskExecutionStats(TimeSpan.FromTicks(1234000), 7, 8192, "AssemblyTaskFactory", null) },
            };
            var data = new WorkerNodeTelemetryData(tasksData, []);

            Dictionary<string, string>? properties = data.GetTasksDetailsProperties();

            properties.ShouldNotBeNull();
            using var doc = System.Text.Json.JsonDocument.Parse(properties!["Tasks"]);
            var task = doc.RootElement[0];

            task.GetProperty("ExecutionsCount").GetInt32().ShouldBe(7);
            task.GetProperty("TotalMemoryBytes").GetInt64().ShouldBe(8192);
            task.GetProperty("TotalMilliseconds").GetDouble().ShouldBe(123.4);
            task.GetProperty("IsCustom").GetBoolean().ShouldBeFalse();
            task.GetProperty("IsNuget").GetBoolean().ShouldBeTrue();
        }

        /// <summary>
        /// <see cref="MockLoggingService"/> that records all <see cref="ILoggingService.LogBuildEvent"/> calls
        /// so tests can inspect emitted build events.
        /// </summary>
        private sealed class EventRecordingLoggingService : MockLoggingService, ILoggingService
        {
            public List<BuildEventArgs> RecordedEvents { get; } = [];

            void ILoggingService.LogBuildEvent(BuildEventArgs buildEvent) => RecordedEvents.Add(buildEvent);
        }
    }
}
