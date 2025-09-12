// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the ThreadSafeTaskRegistry class and task routing functionality.
    /// </summary>
    public class ThreadSafeTaskRegistry_Tests
    {
        /// <summary>
        /// Verify that core MSBuild tasks are in the thread-safe allowlist by default.
        /// </summary>
        [Fact]
        public void CoreTasksAreThreadSafe()
        {
            ThreadSafeTaskRegistry.IsTaskThreadSafe("Message").ShouldBeTrue();
            ThreadSafeTaskRegistry.IsTaskThreadSafe("Warning").ShouldBeTrue();
            ThreadSafeTaskRegistry.IsTaskThreadSafe("Error").ShouldBeTrue();
            ThreadSafeTaskRegistry.IsTaskThreadSafe("ItemGroup").ShouldBeTrue();
            ThreadSafeTaskRegistry.IsTaskThreadSafe("PropertyGroup").ShouldBeTrue();
            ThreadSafeTaskRegistry.IsTaskThreadSafe("CallTarget").ShouldBeTrue();
            ThreadSafeTaskRegistry.IsTaskThreadSafe("MSBuild").ShouldBeTrue();
        }

        /// <summary>
        /// Verify that unknown tasks are not considered thread-safe.
        /// </summary>
        [Fact]
        public void UnknownTasksAreNotThreadSafe()
        {
            ThreadSafeTaskRegistry.IsTaskThreadSafe("UnknownTask").ShouldBeFalse();
            ThreadSafeTaskRegistry.IsTaskThreadSafe("CustomTask").ShouldBeFalse();
            ThreadSafeTaskRegistry.IsTaskThreadSafe("MyTask").ShouldBeFalse();
        }

        /// <summary>
        /// Verify that null and empty task names return false.
        /// </summary>
        [Fact]
        public void NullAndEmptyTaskNamesReturnFalse()
        {
            ThreadSafeTaskRegistry.IsTaskThreadSafe(null).ShouldBeFalse();
            ThreadSafeTaskRegistry.IsTaskThreadSafe(string.Empty).ShouldBeFalse();
            ThreadSafeTaskRegistry.IsTaskThreadSafe("   ").ShouldBeFalse();
        }
    }

    /// <summary>
    /// Tests for task routing functionality in multi-threaded execution mode.
    /// These tests verify both the routing logic and actual task execution behavior.
    /// </summary>
    public class TaskRouting_Tests : IDisposable
    {
        private TestEnvironment _env;

        public TaskRouting_Tests()
        {
            _env = TestEnvironment.Create();
        }

        public void Dispose()
        {
            _env?.Dispose();
        }

        /// <summary>
        /// Verify that thread-safe tasks (like Message) execute in-process when multi-threaded execution is enabled.
        /// </summary>
        [Fact]
        public void ThreadSafeMessageTaskExecutesInProcess()
        {
            var buildParameters = new BuildParameters();
            buildParameters.MultiThreaded = true;
            buildParameters.IsOutOfProc = false;

            var componentHost = new MockHost(buildParameters);
            var requestEntry = CreateMockBuildRequestEntry();
            var taskHost = new TaskHost(componentHost, requestEntry, new MockElementLocation("test.proj"), null);

            // Message task should execute in-process (return false for out-of-process)
            taskHost.ShouldTaskExecuteOutOfProc("Message").ShouldBeFalse();
        }

        /// <summary>
        /// Verify that non-thread-safe tasks (like Copy) execute out-of-process when multi-threaded execution is enabled.
        /// </summary>
        [Fact]
        public void NonThreadSafeCopyTaskExecutesOutOfProcess()
        {
            var buildParameters = new BuildParameters();
            buildParameters.MultiThreaded = true;
            buildParameters.IsOutOfProc = false;

            var componentHost = new MockHost(buildParameters);
            var requestEntry = CreateMockBuildRequestEntry();
            var taskHost = new TaskHost(componentHost, requestEntry, new MockElementLocation("test.proj"), null);

            // Copy task should execute out-of-process (return true for out-of-process)
            taskHost.ShouldTaskExecuteOutOfProc("Copy").ShouldBeTrue();
        }

        /// <summary>
        /// Integration test that verifies Message task routing behavior using actual MSBuild execution.
        /// </summary>
        [Fact]
        public void MessageTaskRoutingIntegrationTest()
        {
            using var buildManager = new BuildManager();
            var projectContent = @"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <Target Name='TestTarget'>
                        <Message Text='This is a test message' Importance='high' />
                    </Target>
                </Project>";

            var projectFile = _env.CreateFile("test.proj", projectContent).Path;

            var parameters = new BuildParameters()
            {
                MultiThreaded = true
            };

            var request = new BuildRequestData(projectFile, new Dictionary<string, string>(), null, new[] { "TestTarget" }, null);
            var result = buildManager.Build(parameters, request);

            // Verify the build succeeded - this confirms the Message task was executed
            result.OverallResult.ShouldBe(BuildResultCode.Success);
        }

        /// <summary>
        /// Integration test that verifies Copy task routing behavior using actual MSBuild execution.
        /// </summary>
        [Fact]
        public void CopyTaskRoutingIntegrationTest()
        {
            using var buildManager = new BuildManager();
            
            var sourceFile = _env.CreateFile("source.txt", "test content").Path;
            var targetFile = Path.Combine(_env.DefaultTestDirectory.Path, "target.txt");
            
            var projectContent = $@"
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <Target Name='TestTarget'>
                        <Copy SourceFiles='{sourceFile}' DestinationFiles='{targetFile}' />
                    </Target>
                </Project>";

            var projectFile = _env.CreateFile("test.proj", projectContent).Path;

            var parameters = new BuildParameters()
            {
                MultiThreaded = true
            };

            var request = new BuildRequestData(projectFile, new Dictionary<string, string>(), null, new[] { "TestTarget" }, null);
            var result = buildManager.Build(parameters, request);

            // Verify the build succeeded - this confirms the Copy task was executed
            result.OverallResult.ShouldBe(BuildResultCode.Success);
            // Verify the file was actually copied
            File.Exists(targetFile).ShouldBeTrue();
        }

        /// <summary>
        /// Verify routing logic works correctly for various scenarios.
        /// </summary>
        [Theory]
        [InlineData(false, false, "Message", false)] // MultiThreaded=false -> default behavior
        [InlineData(false, false, "Copy", false)]    // MultiThreaded=false -> default behavior
        [InlineData(true, true, "Message", true)]    // Already out-of-proc -> stays out-of-proc
        [InlineData(true, true, "Copy", true)]       // Already out-of-proc -> stays out-of-proc
        [InlineData(true, false, "Message", false)]  // Thread-safe task -> in-process
        [InlineData(true, false, "Copy", true)]      // Non-thread-safe task -> out-of-process
        public void TaskRoutingDecisionLogic(bool multiThreaded, bool isOutOfProc, string taskName, bool expectedOutOfProc)
        {
            var buildParameters = new BuildParameters();
            buildParameters.MultiThreaded = multiThreaded;
            buildParameters.IsOutOfProc = isOutOfProc;

            var componentHost = new MockHost(buildParameters);
            var requestEntry = CreateMockBuildRequestEntry();
            var taskHost = new TaskHost(componentHost, requestEntry, new MockElementLocation("test.proj"), null);

            taskHost.ShouldTaskExecuteOutOfProc(taskName).ShouldBe(expectedOutOfProc);
        }

        /// <summary>
        /// Helper method to create a mock BuildRequestEntry.
        /// </summary>
        private static BuildRequestEntry CreateMockBuildRequestEntry()
        {
            var buildRequest = new BuildRequest(1, 1, 1, new[] { "Build" }, null, BuildEventContext.Invalid, null);
            return new BuildRequestEntry(buildRequest, new BuildRequestConfiguration(1, new BuildRequestData("test.proj", new Dictionary<string, string>(), "1.0", Array.Empty<string>(), null), "1.0"));
        }
    }
}