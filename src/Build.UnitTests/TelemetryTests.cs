﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests
{
    public class TelemetryTests
    {
        private readonly ITestOutputHelper _output;

        public TelemetryTests(ITestOutputHelper output)
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

            workerNodeTelemetryData.TasksExecutionData.Keys.ShouldAllBe(k => !k.IsCustom && !k.IsFromNugetCache);
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

            workerNodeTelemetryData.TasksExecutionData.Keys.ShouldAllBe(k => !k.IsFromNugetCache);
        }
    }
}
