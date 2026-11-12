// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
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
    }
}
