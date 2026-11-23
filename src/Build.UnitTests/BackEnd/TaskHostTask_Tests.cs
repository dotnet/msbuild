// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class TaskHostTask_Tests
    {
        [Fact]
        public void HandlesAssemblyResolutionSearchTraceEvent()
        {
            var buildEngine = new MockEngine();
            var searchEvent = new AssemblyResolutionSearchTraceEventArgs(
                "Requested",
                targetProcessorArchitecture: null,
                [
                    new AssemblyResolutionSearchAttempt(
                        "candidate.dll",
                        "search-path",
                        parentAssembly: null,
                        assemblyName: null,
                        AssemblyResolutionSearchResult.FileNotFound,
                        processorArchitecture: null,
                        logAssemblyFoldersEx: false),
                ],
                "ResolveAssemblyReference",
                MessageImportance.Low,
                DateTime.UtcNow);
            var packet = new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(0, searchEvent));

            TaskHostTask.HandleLoggedMessage(buildEngine, packet);

            buildEngine.MessageEvents.ShouldHaveSingleItem().ShouldBeSameAs(searchEvent);
        }

        [Fact]
        public void DrainReceivedPackets_StopsAtTaskCompleteWithoutResettingCompletion()
        {
            var buildEngine = new MockEngine();
            var task = new TaskHostTask(
                ElementLocation.Create("project.proj"),
                taskLoggingContext: null,
                buildComponentHost: null,
                taskHostParameters: TaskHostParameters.Empty,
                taskType: new LoadedType(typeof(DummyTask), AssemblyLoadInfo.Create(typeof(DummyTask).Assembly.FullName, null), typeof(DummyTask).Assembly, typeof(ITaskItem)),
                allowNodeReuse: false,
                forwardConsoleOutput: false,
                projectFile: "project.proj",
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                hostServices: null,
                scheduledNodeId: 1,
                taskEnvironment: TaskEnvironmentHelper.CreateForTest())
            {
                BuildEngine = buildEngine,
            };

            var preCompletionMessage = new BuildMessageEventArgs("before completion", null, nameof(DummyTask), MessageImportance.Normal);
            var postCompletionError = new BuildErrorEventArgs(null, "TEST0001", "project.proj", 1, 1, 0, 0, "after completion", null, nameof(DummyTask));

            task.PacketReceived(0, new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(0, preCompletionMessage)));
            task.PacketReceived(0, new TaskHostTaskComplete(new OutOfProcTaskHostTaskResult(TaskCompleteType.Success), buildProcessEnvironment: null));
            task.PacketReceived(0, new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(0, postCompletionError)));

            task.DrainReceivedPackets().ShouldBeTrue();
            buildEngine.MessageEvents.ShouldHaveSingleItem().ShouldBeSameAs(preCompletionMessage);
            buildEngine.ErrorEvents.ShouldBeEmpty();
        }

        private sealed class DummyTask : ITask
        {
            public IBuildEngine BuildEngine { get; set; }
            public ITaskHost HostObject { get; set; }
            public bool Execute() => true;
        }
    }
}
