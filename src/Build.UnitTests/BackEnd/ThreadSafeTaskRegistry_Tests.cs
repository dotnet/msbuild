// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
        /// Verify that task name matching is case-insensitive.
        /// </summary>
        [Fact]
        public void TaskNameMatchingIsCaseInsensitive()
        {
            ThreadSafeTaskRegistry.IsTaskThreadSafe("message").ShouldBeTrue();
            ThreadSafeTaskRegistry.IsTaskThreadSafe("MESSAGE").ShouldBeTrue();
            ThreadSafeTaskRegistry.IsTaskThreadSafe("MeSSaGe").ShouldBeTrue();
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

        /// <summary>
        /// Verify that tasks can be added to the allowlist.
        /// </summary>
        [Fact]
        public void CanAddTaskToAllowList()
        {
            const string testTaskName = "TestTask";
            
            try
            {
                // Initially should not be thread-safe
                ThreadSafeTaskRegistry.IsTaskThreadSafe(testTaskName).ShouldBeFalse();

                // Add to allowlist
                ThreadSafeTaskRegistry.AddThreadSafeTask(testTaskName);

                // Now should be thread-safe
                ThreadSafeTaskRegistry.IsTaskThreadSafe(testTaskName).ShouldBeTrue();
            }
            finally
            {
                // Clean up
                ThreadSafeTaskRegistry.RemoveThreadSafeTask(testTaskName);
            }
        }

        /// <summary>
        /// Verify that tasks can be removed from the allowlist.
        /// </summary>
        [Fact]
        public void CanRemoveTaskFromAllowList()
        {
            const string testTaskName = "Message"; // Use an existing thread-safe task
            
            try
            {
                // Initially should be thread-safe
                ThreadSafeTaskRegistry.IsTaskThreadSafe(testTaskName).ShouldBeTrue();

                // Remove from allowlist
                ThreadSafeTaskRegistry.RemoveThreadSafeTask(testTaskName);

                // Now should not be thread-safe
                ThreadSafeTaskRegistry.IsTaskThreadSafe(testTaskName).ShouldBeFalse();
            }
            finally
            {
                // Restore the task to allowlist
                ThreadSafeTaskRegistry.AddThreadSafeTask(testTaskName);
            }
        }

        /// <summary>
        /// Verify that the allowlist can be retrieved.
        /// </summary>
        [Fact]
        public void CanGetThreadSafeTaskList()
        {
            var threadSafeTasks = ThreadSafeTaskRegistry.GetThreadSafeTasks();
            
            threadSafeTasks.ShouldNotBeNull();
            threadSafeTasks.Count.ShouldBeGreaterThan(0);
            threadSafeTasks.ShouldContain("Message");
            threadSafeTasks.ShouldContain("Warning");
            threadSafeTasks.ShouldContain("Error");
        }

        /// <summary>
        /// Verify that adding null or empty task names is handled gracefully.
        /// </summary>
        [Fact]
        public void AddingNullOrEmptyTaskNamesIsHandledGracefully()
        {
            // Should not throw exceptions
            ThreadSafeTaskRegistry.AddThreadSafeTask(null);
            ThreadSafeTaskRegistry.AddThreadSafeTask(string.Empty);
            ThreadSafeTaskRegistry.AddThreadSafeTask("   ");

            ThreadSafeTaskRegistry.RemoveThreadSafeTask(null);
            ThreadSafeTaskRegistry.RemoveThreadSafeTask(string.Empty);
            ThreadSafeTaskRegistry.RemoveThreadSafeTask("   ");
        }
    }

    /// <summary>
    /// Tests for task routing functionality in multi-threaded execution mode.
    /// </summary>
    public class TaskRouting_Tests
    {
        /// <summary>
        /// Verify that when multi-threaded execution is disabled, routing uses default behavior.
        /// </summary>
        [Fact]
        public void RoutingUsesDefaultBehaviorWhenMultiThreadedExecutionDisabled()
        {
            var buildParameters = new BuildParameters();
            buildParameters.MultiThreaded = false;
            buildParameters.IsOutOfProc = false;

            var componentHost = new MockHost(buildParameters);
            var requestEntry = CreateMockBuildRequestEntry();
            var taskHost = new TaskHost(componentHost, requestEntry, new MockElementLocation("test.proj"), null);

            // Should use default behavior (false in this case)
            taskHost.ShouldTaskExecuteOutOfProc("Message").ShouldBeFalse();
            taskHost.ShouldTaskExecuteOutOfProc("UnknownTask").ShouldBeFalse();
        }

        /// <summary>
        /// Verify that when already out-of-process, tasks always execute out-of-process.
        /// </summary>
        [Fact]
        public void TasksExecuteOutOfProcessWhenAlreadyOutOfProcess()
        {
            var buildParameters = new BuildParameters();
            buildParameters.MultiThreaded = true;
            buildParameters.IsOutOfProc = true;

            var componentHost = new MockHost(buildParameters);
            var requestEntry = CreateMockBuildRequestEntry();
            var taskHost = new TaskHost(componentHost, requestEntry, new MockElementLocation("test.proj"), null);

            // Should always be out-of-process when already out-of-process
            taskHost.ShouldTaskExecuteOutOfProc("Message").ShouldBeTrue();
            taskHost.ShouldTaskExecuteOutOfProc("UnknownTask").ShouldBeTrue();
        }

        /// <summary>
        /// Verify that thread-safe tasks execute in-process when multi-threaded execution is enabled.
        /// </summary>
        [Fact]
        public void ThreadSafeTasksExecuteInProcessWhenMultiThreadedExecutionEnabled()
        {
            var buildParameters = new BuildParameters();
            buildParameters.MultiThreaded = true;
            buildParameters.IsOutOfProc = false;

            var componentHost = new MockHost(buildParameters);
            var requestEntry = CreateMockBuildRequestEntry();
            var taskHost = new TaskHost(componentHost, requestEntry, new MockElementLocation("test.proj"), null);

            // Thread-safe tasks should execute in-process (return false for out-of-process)
            taskHost.ShouldTaskExecuteOutOfProc("Message").ShouldBeFalse();
            taskHost.ShouldTaskExecuteOutOfProc("Warning").ShouldBeFalse();
            taskHost.ShouldTaskExecuteOutOfProc("Error").ShouldBeFalse();
        }

        /// <summary>
        /// Verify that non-thread-safe tasks execute out-of-process when multi-threaded execution is enabled.
        /// </summary>
        [Fact]
        public void NonThreadSafeTasksExecuteOutOfProcessWhenMultiThreadedExecutionEnabled()
        {
            var buildParameters = new BuildParameters();
            buildParameters.MultiThreaded = true;
            buildParameters.IsOutOfProc = false;

            var componentHost = new MockHost(buildParameters);
            var requestEntry = CreateMockBuildRequestEntry();
            var taskHost = new TaskHost(componentHost, requestEntry, new MockElementLocation("test.proj"), null);

            // Non-thread-safe tasks should execute out-of-process (return true for out-of-process)
            taskHost.ShouldTaskExecuteOutOfProc("UnknownTask").ShouldBeTrue();
            taskHost.ShouldTaskExecuteOutOfProc("CustomTask").ShouldBeTrue();
            taskHost.ShouldTaskExecuteOutOfProc("ThirdPartyTask").ShouldBeTrue();
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