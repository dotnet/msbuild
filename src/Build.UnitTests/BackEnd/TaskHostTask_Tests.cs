// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
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
            var task = new TaskHostTask(
                taskLocation: null,
                taskLoggingContext: null,
                buildComponentHost: null,
                TaskHostParameters.Empty,
                new LoadedType(
                    typeof(TestTask),
                    AssemblyLoadInfo.Create(typeof(TestTask).Assembly.FullName, null),
                    typeof(TestTask).Assembly,
                    typeof(ITaskItem)),
                useSidecarTaskHost: false,
                projectFile: null,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                hostServices: null,
                scheduledNodeId: 1,
                TaskEnvironmentHelper.CreateForTest())
            {
                BuildEngine = buildEngine,
            };
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
                new AssemblyResolutionSearchTraceMessageFormats(
                    "Search {0}",
                    "Search {0} from {1}",
                    "Searched AssemblyFoldersEx",
                    "Missing {0}",
                    "Found {1} at {0}, expected {2}",
                    "No identity {0}",
                    "Not in GAC {0}",
                    "Not a file {0}",
                    "Architecture {1} at {0}, expected {2}"),
                "ResolveAssemblyReference",
                MessageImportance.Low,
                DateTime.UtcNow);
            var packet = new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(0, searchEvent));

            task.HandleLoggedMessage(packet);

            buildEngine.MessageEvents.ShouldHaveSingleItem().ShouldBeSameAs(searchEvent);
        }

        private sealed class TestTask : ITask
        {
            public IBuildEngine BuildEngine { get; set; }

            public ITaskHost HostObject { get; set; }

            public bool Execute() => true;
        }
    }
}
