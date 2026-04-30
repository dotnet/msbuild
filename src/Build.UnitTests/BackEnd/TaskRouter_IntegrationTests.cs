// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Integration tests for task routing in multi-threaded mode.
    /// Tests verify that tasks with MSBuildMultiThreadableTaskAttribute (non-inheritable)
    /// run in-process, while tasks without this attribute run in TaskHost for isolation.
    /// Tasks may also implement IMultiThreadableTask to gain access to TaskEnvironment APIs.
    /// </summary>
    public class TaskRouter_IntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;
        private readonly string _testProjectsDir;

        public TaskRouter_IntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(output);

            // Create directory for test projects
            _testProjectsDir = _env.CreateFolder().Path;
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        /// <summary>
        /// Verifies that a NonEnlightened task (no interface, no attribute) runs in TaskHost
        /// when MultiThreaded mode is enabled.
        /// </summary>
        [Fact]
        public void NonEnlightenedTask_RunsInTaskHost_InMultiThreadedMode()
        {
            // Arrange
            string projectContent = CreateTestProject(
                taskName: "NonEnlightenedTestTask",
                taskClass: "NonEnlightenedTask");

            string projectFile = Path.Combine(_testProjectsDir, "NonEnlightenedTaskProject.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = true,
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Verify task was launched in TaskHost
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "NonEnlightenedTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("NonEnlightenedTask executed");
        }

        /// <summary>
        /// Verifies that a task with IMultiThreadableTask interface but without MSBuildMultiThreadableTaskAttribute
        /// runs in TaskHost when MultiThreaded mode is enabled. Only the attribute determines routing.
        /// </summary>
        [Fact]
        public void TaskWithInterface_RunsInTaskHost_InMultiThreadedMode()
        {
            // Arrange
            string projectContent = CreateTestProject(
                taskName: "InterfaceTestTask",
                taskClass: "TaskWithInterface");

            string projectFile = Path.Combine(_testProjectsDir, "InterfaceTaskProject.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = true,
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Verify task was launched in TaskHost (interface alone is not sufficient)
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "InterfaceTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("TaskWithInterface executed");
        }

        /// <summary>
        /// Verifies that a task with MSBuildMultiThreadableTaskAttribute runs in-process
        /// (not in TaskHost) when MultiThreaded mode is enabled.
        /// </summary>
        [Fact]
        public void TaskWithAttribute_RunsInProcess_InMultiThreadedMode()
        {
            // Arrange
            string projectContent = CreateTestProject(
                taskName: "AttributeTestTask",
                taskClass: "TaskWithAttribute");

            string projectFile = Path.Combine(_testProjectsDir, "AttributeTaskProject.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = true,
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Verify task was NOT launched in TaskHost (runs in-process)
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "AttributeTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("TaskWithAttribute executed");
        }

        /// <summary>
        /// Verifies that when MultiThreaded mode is disabled, even NonEnlightened tasks
        /// run in-process and do not use TaskHost.
        /// </summary>
        [Fact]
        public void NonEnlightenedTask_RunsInProcess_WhenMultiThreadedModeDisabled()
        {
            // Arrange
            string projectContent = CreateTestProject(
                taskName: "NonEnlightenedTestTask",
                taskClass: "NonEnlightenedTask");

            string projectFile = Path.Combine(_testProjectsDir, "NonEnlightenedTaskSingleThreaded.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = false, // Single-threaded mode
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Verify task was NOT launched in TaskHost (runs in-process even though it's NonEnlightened)
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "NonEnlightenedTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("NonEnlightenedTask executed");
        }

        /// <summary>
        /// Verifies that all tasks run in-process in single-threaded mode regardless of attributes.
        /// </summary>
        [Fact]
        public void TaskWithInterface_RunsInProcess_WhenMultiThreadedModeDisabled()
        {
            // Arrange
            string projectContent = CreateTestProject(
                taskName: "InterfaceTestTask",
                taskClass: "TaskWithInterface");

            string projectFile = Path.Combine(_testProjectsDir, "InterfaceTaskSingleThreaded.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = false, // Single-threaded mode
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Verify task was NOT launched in TaskHost
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "InterfaceTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("TaskWithInterface executed");
        }

        /// <summary>
        /// Verifies that multiple task types in the same build are routed correctly
        /// based on their characteristics in multi-threaded mode.
        /// </summary>
        [Fact]
        public void MixedTasks_RouteCorrectly_InMultiThreadedMode()
        {
            // Arrange
            string projectContent = $@"
<Project>
    <UsingTask TaskName=""NonEnlightenedTestTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    <UsingTask TaskName=""InterfaceTestTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    <UsingTask TaskName=""AttributeTestTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    
    <Target Name=""TestTarget"">
        <NonEnlightenedTestTask />
        <InterfaceTestTask />
        <AttributeTestTask />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, "MixedTasksProject.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = true,
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // NonEnlightenedTask and InterfaceTask should use TaskHost
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "NonEnlightenedTestTask");
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "InterfaceTestTask");

            // Only Attribute task should NOT use TaskHost
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "AttributeTestTask");

            // All tasks should execute successfully
            logger.FullLog.ShouldContain("NonEnlightenedTask executed");
            logger.FullLog.ShouldContain("TaskWithInterface executed");
            logger.FullLog.ShouldContain("TaskWithAttribute executed");
        }

        /// <summary>
        /// Verifies that explicit TaskHostFactory request overrides routing logic,
        /// forcing tasks to run in TaskHost even if they have the MSBuildMultiThreadableTaskAttribute.
        /// </summary>
        [Fact]
        public void ExplicitTaskHostFactory_OverridesRoutingLogic()
        {
            // Arrange - Use a task with attribute but explicitly request TaskHostFactory
            string projectContent = $@"
<Project>
    <UsingTask TaskName=""AttributeTestTask"" 
               AssemblyFile=""{Assembly.GetExecutingAssembly().Location}""
               TaskFactory=""TaskHostFactory"" />
    
    <Target Name=""TestTarget"">
        <AttributeTestTask />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, "ExplicitTaskHostFactory.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = true,
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Task should use TaskHost because TaskHostFactory was explicitly requested
            // This overrides the normal routing logic which would run attribute tasks in-process
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "AttributeTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("TaskWithAttribute executed");
        }

        /// <summary>
        /// Verifies that TaskRouter.IsKnownProblematicTask returns true for NuGet.Build.Tasks.RestoreTask.
        /// </summary>
        [Fact]
        public void IsKnownProblematicTask_ReturnsTrueForRestoreTask()
        {
            TaskRouter.IsKnownProblematicTask(typeof(global::NuGet.Build.Tasks.RestoreTask)).ShouldBeTrue();
        }

        /// <summary>
        /// Verifies that TaskRouter.IsKnownProblematicTask returns false for regular tasks.
        /// </summary>
        [Fact]
        public void IsKnownProblematicTask_ReturnsFalseForRegularTask()
        {
            TaskRouter.IsKnownProblematicTask(typeof(NonEnlightenedTestTask)).ShouldBeFalse();
            TaskRouter.IsKnownProblematicTask(typeof(AttributeTestTask)).ShouldBeFalse();
        }

        /// <summary>
        /// Verifies that a known problematic task (RestoreTask) is routed to TaskHost
        /// in multi-threaded mode.
        /// </summary>
        [Fact]
        public void ProblematicTask_RoutedToTaskHost_InMultiThreadedMode()
        {
            // Arrange
            string projectContent = $@"
<Project>
    <UsingTask TaskName=""RestoreTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    
    <Target Name=""TestTarget"">
        <RestoreTask />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, "RestoreTaskMT.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = true,
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "RestoreTask");
            logger.FullLog.ShouldContain("RestoreTask executed");
        }

        /// <summary>
        /// Verifies that a known problematic task (RestoreTask) is routed to TaskHost
        /// when the current process was launched in MSBuild Server mode (even without /mt).
        /// The workaround branch checks the <see cref="Traits.OriginalUseMSBuildServerEnvVarName"/>
        /// sidecar env var (set by NodeLauncher.DisableMSBuildServer in production) rather than
        /// MSBUILDUSESERVER, because the latter is intentionally zeroed in spawned child processes.
        /// See https://github.com/dotnet/msbuild/issues/13315.
        /// </summary>
        [Fact]
        public void ProblematicTask_RoutedToTaskHost_InServerMode()
        {
            // Arrange: simulate the NodeLauncher having captured an original "1" before zeroing.
            _env.SetEnvironmentVariable(Traits.OriginalUseMSBuildServerEnvVarName, "1");

            string projectContent = $@"
<Project>
    <UsingTask TaskName=""RestoreTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    
    <Target Name=""TestTarget"">
        <RestoreTask />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, "RestoreTaskServer.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = false,
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "RestoreTask");
            logger.FullLog.ShouldContain("RestoreTask executed");
        }

        /// <summary>
        /// Verifies the actual behavioural guarantee of the workaround for
        /// https://github.com/dotnet/msbuild/issues/13315: two consecutive invocations
        /// of a known problematic task (RestoreTask) execute in DIFFERENT OS processes,
        /// ensuring static singleton state (e.g. NuGet's PluginManager,
        /// EnvironmentVariableWrapper) cannot leak across calls.
        ///
        /// This guards against a regression where the workaround's transient-TaskHost
        /// route is replaced with a sidecar TaskHost: the existing
        /// <see cref="ProblematicTask_RoutedToTaskHost_InMultiThreadedMode"/> test
        /// would still pass (the task is still "in a TaskHost"), but a long-lived
        /// sidecar would defeat the entire purpose of the workaround.
        ///
        /// EnableNodeReuse is intentionally true: a sidecar TaskHost spawned without
        /// the workaround would be reused across both calls (same PID), whereas the
        /// workaround forces nodeReuse=false on the spawned host, producing a fresh
        /// process per call.
        /// </summary>
        [Fact]
        public void ProblematicTask_GetsFreshProcess_OnEachInvocation_InMultiThreadedMode()
        {
            // Arrange: two RestoreTask invocations in the same target.
            string projectContent = $@"
<Project>
    <UsingTask TaskName=""RestoreTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />

    <Target Name=""TestTarget"">
        <RestoreTask />
        <RestoreTask />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, "RestoreTaskFreshProcess.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = true,
                Loggers = new[] { logger },
                DisableInProcNode = false,

                // Intentionally true: this is what makes the test meaningful. Without
                // the workaround, a sidecar TaskHost with nodeReuse=true would be
                // reused for the second call (same PID). With the workaround,
                // useSidecarTaskHost=false forces nodeReuse=false on the spawned
                // host, so each call gets a fresh process.
                EnableNodeReuse = true,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Both invocations must have routed through a TaskHost (rather than the in-proc node).
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "RestoreTask");

            // Extract the PIDs reported by each invocation.
            int[] pids = ExtractReportedPids(logger.FullLog);

            pids.Length.ShouldBe(2, $"Expected two RestoreTask invocations to log a PID. Log:{Environment.NewLine}{logger.FullLog}");
            pids[0].ShouldNotBe(pids[1], "Each invocation must run in a fresh OS process so static state cannot leak across calls.");
            pids.ShouldNotContain(Process.GetCurrentProcess().Id, "TaskHost should be out-of-process from the test runner.");
        }

        /// <summary>
        /// Server-mode counterpart to <see cref="ProblematicTask_GetsFreshProcess_OnEachInvocation_InMultiThreadedMode"/>.
        /// Verifies the same behavioural guarantee — two consecutive RestoreTask invocations
        /// run in different OS processes so static singleton state cannot leak across calls —
        /// when the trigger is "current process is the MSBuild Server" rather than /mt.
        ///
        /// This guards against a regression where the workaround's transient-TaskHost route
        /// is preserved for /mt mode but accidentally broken for server mode (or vice versa):
        /// the routing-only test (<see cref="ProblematicTask_RoutedToTaskHost_InServerMode"/>)
        /// would still pass because the task is "in a TaskHost", but a sidecar TaskHost with
        /// node reuse would defeat the entire purpose of the workaround.
        ///
        /// EnableNodeReuse is intentionally true: see the corresponding comment on the /mt
        /// counterpart for why this is load-bearing for the test's correctness.
        /// </summary>
        [Fact]
        public void ProblematicTask_GetsFreshProcess_OnEachInvocation_InServerMode()
        {
            // Arrange: simulate the NodeLauncher having captured an original "1" before zeroing.
            _env.SetEnvironmentVariable(Traits.OriginalUseMSBuildServerEnvVarName, "1");

            string projectContent = $@"
<Project>
    <UsingTask TaskName=""RestoreTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />

    <Target Name=""TestTarget"">
        <RestoreTask />
        <RestoreTask />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, "RestoreTaskFreshProcessServer.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = false,
                Loggers = new[] { logger },
                DisableInProcNode = false,

                // Intentionally true: see the corresponding test for /mt mode for the
                // reasoning. Without nodeReuse=true the test cannot distinguish "sidecar
                // TaskHost reused" from "transient TaskHost spawned fresh", so it would
                // pass even if the workaround silently regressed for server mode.
                EnableNodeReuse = true,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Both invocations must have routed through a TaskHost (rather than the in-proc node).
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "RestoreTask");

            // Extract the PIDs reported by each invocation.
            int[] pids = ExtractReportedPids(logger.FullLog);

            pids.Length.ShouldBe(2, $"Expected two RestoreTask invocations to log a PID. Log:{Environment.NewLine}{logger.FullLog}");
            pids[0].ShouldNotBe(pids[1], "Each invocation must run in a fresh OS process so static state cannot leak across calls.");
            pids.ShouldNotContain(Process.GetCurrentProcess().Id, "TaskHost should be out-of-process from the test runner.");
        }

        /// <summary>
        /// Extracts the OS PIDs reported by each invocation of the fake RestoreTask
        /// (defined below as <see cref="global::NuGet.Build.Tasks.RestoreTask"/>),
        /// which logs <c>"RestoreTask executed in PID=&lt;n&gt;"</c> on every call.
        /// </summary>
        private static int[] ExtractReportedPids(string log)
        {
            var pids = new List<int>();
            foreach (Match m in Regex.Matches(log, @"RestoreTask executed in PID=(\d+)"))
            {
                pids.Add(int.Parse(m.Groups[1].Value));
            }

            return pids.ToArray();
        }

        /// <summary>
        /// Verifies that a known problematic task (RestoreTask) runs in-process
        /// when neither multi-threaded mode nor server mode is active.
        /// </summary>
        [Fact]
        public void ProblematicTask_RunsInProcess_WhenNoMTOrServer()
        {
            // Arrange
            string projectContent = $@"
<Project>
    <UsingTask TaskName=""RestoreTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    
    <Target Name=""TestTarget"">
        <RestoreTask />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, "RestoreTaskNoMT.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = false,
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            // Act
            var buildManager = BuildManager.DefaultBuildManager;
            var result = buildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Should run in-process when neither MT nor server mode
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "RestoreTask");
            logger.FullLog.ShouldContain("RestoreTask executed");
        }

        private string CreateTestProject(string taskName, string taskClass)
        {
            return $@"
<Project>
    <UsingTask TaskName=""{taskName}"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    
    <Target Name=""TestTarget"">
        <{taskName} />
    </Target>
</Project>";
        }
    }

    /// <summary>
    /// Helper utilities for testing task routing behavior.
    /// Provides robust assertions that are less fragile than raw log string matching.
    /// </summary>
    internal static class TaskRouterTestHelper
    {
        /// <summary>
        /// Asserts that a task was launched in an external TaskHost process.
        /// </summary>
        /// <param name="logger">The build logger containing execution logs.</param>
        /// <param name="taskName">The name of the task to verify.</param>
        public static void AssertTaskUsedTaskHost(MockLogger logger, string taskName)
        {
            // Look for the distinctive "Launching task" message that indicates TaskHost usage
            string launchingMessage = $"Launching task \"{taskName}\"";
            logger.FullLog.ShouldContain(launchingMessage);
            logger.FullLog.ShouldContain("external task host");
        }

        /// <summary>
        /// Asserts that a task ran in-process (not in TaskHost).
        /// </summary>
        /// <param name="logger">The build logger containing execution logs.</param>
        /// <param name="taskName">The name of the task to verify.</param>
        public static void AssertTaskRanInProcess(MockLogger logger, string taskName)
        {
            // Verify the "Launching task" message does NOT appear for this task
            string launchingMessage = $"Launching task \"{taskName}\"";
            logger.FullLog.ShouldNotContain(launchingMessage);
        }
    }

    #region Test Task Implementations

    /// <summary>
    /// NonEnlightened task without IMultiThreadableTask interface or MSBuildMultiThreadableTaskAttribute.
    /// Should run in TaskHost in multi-threaded mode.
    /// </summary>
    public class NonEnlightenedTestTask : Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "NonEnlightenedTask executed");
            return true;
        }
    }

    /// <summary>
    /// Task implementing IMultiThreadableTask interface.
    /// Should run in-process in multi-threaded mode.
    /// </summary>
    public class InterfaceTestTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "TaskWithInterface executed");
            return true;
        }
    }



    /// <summary>
    /// Task marked with MSBuildMultiThreadableTaskAttribute.
    /// Should run in-process in multi-threaded mode.
    /// </summary>
    /// <remarks>
    /// Uses the public test version of MSBuildMultiThreadableTaskAttribute defined in this file,
    /// which shadows the internal Framework version intentionally for testing.
    /// </remarks>
#pragma warning disable CS0436 // Type conflicts with imported type - intentional for testing
    [MSBuildMultiThreadableTask]
#pragma warning restore CS0436
    public class AttributeTestTask : Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "TaskWithAttribute executed");
            return true;
        }
    }

    #endregion
}

// Test task in the NuGet.Build.Tasks namespace to simulate the real RestoreTask for routing tests.
// TaskRouter identifies problematic tasks by full type name.
namespace NuGet.Build.Tasks
{
    /// <summary>
    /// Simulates the NuGet RestoreTask for testing task routing workaround.
    /// Has the same full name (NuGet.Build.Tasks.RestoreTask) that TaskRouter checks.
    /// </summary>
    public class RestoreTask : Microsoft.Build.Utilities.Task
    {
        public override bool Execute()
        {
            // Include the OS PID so tests can verify each invocation runs in a fresh
            // TaskHost process (the core behavioural guarantee of the workaround for
            // https://github.com/dotnet/msbuild/issues/13315).
            Log.LogMessage(MessageImportance.High, $"RestoreTask executed in PID={Process.GetCurrentProcess().Id}");
            return true;
        }
    }
}

// Custom attribute definition in Microsoft.Build.Framework namespace to match what TaskRouter expects
// TaskRouter looks for attributes with FullName = "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute"
// Since the real attribute is internal in Framework, we define our own test version here
namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Test attribute to mark tasks as safe for multi-threaded execution.
    /// This is a test copy in this test assembly that will be recognized
    /// by name-based attribute detection in TaskRouter.
    /// Must match the non-inheritable definition (Inherited = false).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class MSBuildMultiThreadableTaskAttribute : Attribute
    {
    }
}
