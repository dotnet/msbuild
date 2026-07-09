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

        [Fact]
        public void RequiresTransientTaskHost_ReturnsTrueForRestoreTask()
        {
            TaskRouter.RequiresTransientTaskHost(typeof(NuGet.Build.Tasks.RestoreTask)).ShouldBeTrue();
        }

        [Fact]
        public void RequiresTransientTaskHost_ReturnsFalseForRegularTask()
        {
            TaskRouter.RequiresTransientTaskHost(typeof(NonEnlightenedTestTask)).ShouldBeFalse();
            TaskRouter.RequiresTransientTaskHost(typeof(AttributeTestTask)).ShouldBeFalse();
        }

        [Fact]
        public void RequiresTransientTaskHost_RoutedToTaskHost_InMultiThreadedMode()
        {
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
                Loggers = [logger],
                DisableInProcNode = false,
                EnableNodeReuse = false,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                ["TestTarget"],
                null);

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            BuildResult result = buildManager.Build(buildParameters, buildRequestData);

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "RestoreTask");
            logger.FullLog.ShouldContain("RestoreTask executed");
        }

        [Fact]
        public void RequiresTransientTaskHost_RoutedToTaskHost_InServerMode()
        {
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
                Loggers = [logger],
                DisableInProcNode = false,
                EnableNodeReuse = false,

                // Simulate running under the MSBuild Server. In production this flag is
                // set process-wide by OutOfProcServerNode via BuildParameters.MarkProcessAsLongLivedHost.
                IsLongLivedHost = true,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                ["TestTarget"],
                null);

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            BuildResult result = buildManager.Build(buildParameters, buildRequestData);

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "RestoreTask");
            logger.FullLog.ShouldContain("RestoreTask executed");
        }

        [Fact]
        public void RequiresTransientTaskHost_GetsFreshProcess_OnEachInvocation_InMultiThreadedMode()
        {
            string projectContent = $@"
<Project>
    <UsingTask TaskName=""RestoreTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />

    <Target Name=""TestTarget"">
        <RestoreTask />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, "RestoreTaskFreshProcess.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = true,
                Loggers = [logger],
                DisableInProcNode = false,

                // Reuse must stay ON so we test the workaround, not natural process death.
                // With reuse off the test cannot distinguish "transient TaskHost (workaround)"
                // from "sidecar TaskHost that happened to die between builds".
                EnableNodeReuse = true,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                ["TestTarget"],
                null);

            // Two separate Build cycles. The workaround forces nodeReuse=false on the spawned
            // TaskHost so it dies at EndBuild, giving the next Build a fresh process.
            BuildManager buildManager = BuildManager.DefaultBuildManager;
            BuildResult result1 = buildManager.Build(buildParameters, buildRequestData);
            BuildResult result2 = buildManager.Build(buildParameters, buildRequestData);

            result1.OverallResult.ShouldBe(BuildResultCode.Success);
            result2.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "RestoreTask");

            int[] pids = ExtractReportedPids(logger.FullLog);

            pids.Length.ShouldBe(2, $"Expected two RestoreTask invocations to log a PID. Log:{Environment.NewLine}{logger.FullLog}");
            pids[0].ShouldNotBe(pids[1], "Each build must spawn a fresh TaskHost so NuGet static state cannot leak across builds.");
            pids.ShouldNotContain(Process.GetCurrentProcess().Id, "TaskHost should be out-of-process from the test runner.");
        }

        [Fact]
        public void RequiresTransientTaskHost_GetsFreshProcess_OnEachInvocation_InServerMode()
        {
            string projectContent = $@"
<Project>
    <UsingTask TaskName=""RestoreTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />

    <Target Name=""TestTarget"">
        <RestoreTask />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, "RestoreTaskFreshProcessServer.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = false,
                Loggers = [logger],
                DisableInProcNode = false,

                // Reuse must stay ON so we test the workaround, not natural process death.
                EnableNodeReuse = true,

                // Simulate running under the MSBuild Server.
                IsLongLivedHost = true,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                ["TestTarget"],
                null);

            // Two separate Build cycles. The workaround forces nodeReuse=false on the spawned
            // TaskHost so it dies at EndBuild, giving the next Build a fresh process.
            BuildManager buildManager = BuildManager.DefaultBuildManager;
            BuildResult result1 = buildManager.Build(buildParameters, buildRequestData);
            BuildResult result2 = buildManager.Build(buildParameters, buildRequestData);

            result1.OverallResult.ShouldBe(BuildResultCode.Success);
            result2.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "RestoreTask");

            int[] pids = ExtractReportedPids(logger.FullLog);

            pids.Length.ShouldBe(2, $"Expected two RestoreTask invocations to log a PID. Log:{Environment.NewLine}{logger.FullLog}");
            pids[0].ShouldNotBe(pids[1], "Each build must spawn a fresh TaskHost so NuGet static state cannot leak across builds.");
            pids.ShouldNotContain(Process.GetCurrentProcess().Id, "TaskHost should be out-of-process from the test runner.");
        }

        private static int[] ExtractReportedPids(string log)
        {
            var pids = new List<int>();
            foreach (Match m in Regex.Matches(log, @"RestoreTask executed in PID=(\d+)"))
            {
                pids.Add(int.Parse(m.Groups[1].Value));
            }

            return pids.ToArray();
        }

        [Fact]
        public void RequiresTransientTaskHost_RunsInProcess_WhenNoMTOrServer()
        {
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
                Loggers = [logger],
                DisableInProcNode = false,
                EnableNodeReuse = false,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                ["TestTarget"],
                null);

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            BuildResult result = buildManager.Build(buildParameters, buildRequestData);

            result.OverallResult.ShouldBe(BuildResultCode.Success);

            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "RestoreTask");
            logger.FullLog.ShouldContain("RestoreTask executed");
        }

        #region UsingTask TaskHostRouting attribute (prototype) tests

        /// <summary>
        /// PROTOTYPE. A task that would normally be routed to a TaskHost (no thread-safety attribute)
        /// is forced in-process by TaskHostRouting="InProc" on its UsingTask.
        /// </summary>
        [Fact]
        public void UsingTaskRouting_InProc_RunsInProcess_InMultiThreadedMode()
        {
            string projectContent = CreateTestProjectWithRouting("PidLoggingTestTask", "InProc");
            string projectFile = Path.Combine(_testProjectsDir, "RoutingInProc.proj");
            File.WriteAllText(projectFile, projectContent);

            MockLogger logger = BuildRoutingProject(projectFile, multiThreaded: true, out BuildResult result);

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "PidLoggingTestTask");

            int[] pids = ExtractReportedPids(logger.FullLog, "PidLoggingTestTask");
            pids.Length.ShouldBe(1, $"Expected one invocation. Log:{Environment.NewLine}{logger.FullLog}");
            pids[0].ShouldBe(Process.GetCurrentProcess().Id, "Task pinned to InProc must run in the build process.");
        }

        /// <summary>
        /// PROTOTYPE. A task that would normally run in-process (marked with the thread-safety attribute)
        /// is forced into a sidecar TaskHost by TaskHostRouting="Sidecar".
        /// </summary>
        [Fact]
        public void UsingTaskRouting_Sidecar_RunsInTaskHost_InMultiThreadedMode()
        {
            string projectContent = CreateTestProjectWithRouting("PidLoggingMultiThreadableTestTask", "Sidecar");
            string projectFile = Path.Combine(_testProjectsDir, "RoutingSidecar.proj");
            File.WriteAllText(projectFile, projectContent);

            MockLogger logger = BuildRoutingProject(projectFile, multiThreaded: true, out BuildResult result);

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "PidLoggingMultiThreadableTestTask");

            int[] pids = ExtractReportedPids(logger.FullLog, "PidLoggingMultiThreadableTestTask");
            pids.Length.ShouldBe(1, $"Expected one invocation. Log:{Environment.NewLine}{logger.FullLog}");
            pids[0].ShouldNotBe(Process.GetCurrentProcess().Id, "Task pinned to Sidecar must run out of process.");
        }

        /// <summary>
        /// PROTOTYPE. A task pinned to a transient TaskHost by TaskHostRouting="Transient" gets a fresh
        /// process on each build, even with node reuse enabled.
        /// </summary>
        [Fact]
        public void UsingTaskRouting_Transient_GetsFreshProcess_InMultiThreadedMode()
        {
            string projectContent = CreateTestProjectWithRouting("PidLoggingMultiThreadableTestTask", "Transient");
            string projectFile = Path.Combine(_testProjectsDir, "RoutingTransient.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = true,
                Loggers = [logger],
                DisableInProcNode = false,

                // Reuse stays ON: a transient TaskHost must still die between builds.
                EnableNodeReuse = true,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                ["TestTarget"],
                null);

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            BuildResult result1 = buildManager.Build(buildParameters, buildRequestData);
            BuildResult result2 = buildManager.Build(buildParameters, buildRequestData);

            result1.OverallResult.ShouldBe(BuildResultCode.Success);
            result2.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "PidLoggingMultiThreadableTestTask");

            int[] pids = ExtractReportedPids(logger.FullLog, "PidLoggingMultiThreadableTestTask");
            pids.Length.ShouldBe(2, $"Expected two invocations to log a PID. Log:{Environment.NewLine}{logger.FullLog}");
            pids[0].ShouldNotBe(pids[1], "Each build must spawn a fresh transient TaskHost.");
            pids.ShouldNotContain(Process.GetCurrentProcess().Id, "Transient TaskHost must be out-of-process.");
        }

        /// <summary>
        /// PROTOTYPE. The TaskHostRouting override has no effect outside multi-threaded mode.
        /// </summary>
        [Fact]
        public void UsingTaskRouting_Ignored_WhenNotMultiThreaded()
        {
            string projectContent = CreateTestProjectWithRouting("PidLoggingMultiThreadableTestTask", "Sidecar");
            string projectFile = Path.Combine(_testProjectsDir, "RoutingIgnored.proj");
            File.WriteAllText(projectFile, projectContent);

            MockLogger logger = BuildRoutingProject(projectFile, multiThreaded: false, out BuildResult result);

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "PidLoggingMultiThreadableTestTask");
        }

        /// <summary>
        /// PROTOTYPE. An invalid TaskHostRouting value is rejected during evaluation.
        /// </summary>
        [Fact]
        public void UsingTaskRouting_InvalidValue_FailsEvaluation()
        {
            string projectContent = CreateTestProjectWithRouting("PidLoggingTestTask", "Bogus");
            string projectFile = Path.Combine(_testProjectsDir, "RoutingInvalid.proj");
            File.WriteAllText(projectFile, projectContent);

            MockLogger logger = BuildRoutingProject(projectFile, multiThreaded: true, out BuildResult result);

            result.OverallResult.ShouldBe(BuildResultCode.Failure);

            // The attribute name is an invariant token in the InvalidAttributeValue diagnostic.
            logger.FullLog.ShouldContain("TaskHostRouting");
        }

        /// <summary>
        /// PROTOTYPE. TaskHostRouting values map to the expected routing override (case-insensitive),
        /// and unknown/empty values yield no override.
        /// </summary>
        [Theory]
        [InlineData("InProc", nameof(TaskHostRoutingOverride.InProc))]
        [InlineData("inproc", nameof(TaskHostRoutingOverride.InProc))]
        [InlineData("Sidecar", nameof(TaskHostRoutingOverride.Sidecar))]
        [InlineData("Transient", nameof(TaskHostRoutingOverride.Transient))]
        [InlineData("", nameof(TaskHostRoutingOverride.None))]
        [InlineData("Bogus", nameof(TaskHostRoutingOverride.None))]
        public void ParseRoutingOverride_MapsValues(string value, string expected)
        {
            TaskRouter.ParseRoutingOverride(value).ToString().ShouldBe(expected);
        }

        /// <summary>
        /// PROTOTYPE. The TaskHostRouting attribute round-trips through the construction object model.
        /// </summary>
        [Fact]
        public void ProjectUsingTaskElement_TaskHostRouting_RoundTrips()
        {
            var root = Microsoft.Build.Construction.ProjectRootElement.Create();
            Microsoft.Build.Construction.ProjectUsingTaskElement usingTask = root.AddUsingTask("MyTask", "MyAssembly.dll", null);

            usingTask.TaskHostRouting.ShouldBe(string.Empty);

            usingTask.TaskHostRouting = "Sidecar";
            usingTask.TaskHostRouting.ShouldBe("Sidecar");
        }

        #endregion

        private MockLogger BuildRoutingProject(string projectFile, bool multiThreaded, out BuildResult result)
        {
            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = multiThreaded,
                Loggers = [logger],
                DisableInProcNode = false,
                EnableNodeReuse = false,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                ["TestTarget"],
                null);

            BuildManager buildManager = BuildManager.DefaultBuildManager;
            result = buildManager.Build(buildParameters, buildRequestData);
            return logger;
        }

        private string CreateTestProjectWithRouting(string taskName, string routing)
        {
            return $@"
<Project>
    <UsingTask TaskName=""{taskName}"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" TaskHostRouting=""{routing}"" />

    <Target Name=""TestTarget"">
        <{taskName} />
    </Target>
</Project>";
        }

        private static int[] ExtractReportedPids(string log, string taskName)
        {
            var pids = new List<int>();
            foreach (Match m in Regex.Matches(log, Regex.Escape(taskName) + @" executed in PID=(\d+)"))
            {
                pids.Add(int.Parse(m.Groups[1].Value));
            }

            return pids.ToArray();
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

    /// <summary>
    /// PROTOTYPE. A non-enlightened task (no attribute, normally routed to a TaskHost in
    /// multi-threaded mode) that logs the PID of the process it runs in, so tests can verify the
    /// UsingTask TaskHostRouting override actually changed where it executed.
    /// </summary>
    public class PidLoggingTestTask : Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, $"PidLoggingTestTask executed in PID={Process.GetCurrentProcess().Id}");
            return true;
        }
    }

    /// <summary>
    /// PROTOTYPE. A task marked with MSBuildMultiThreadableTaskAttribute (normally runs in-process in
    /// multi-threaded mode) that logs its PID, used to verify the UsingTask TaskHostRouting override
    /// can push it into a sidecar or transient TaskHost.
    /// </summary>
#pragma warning disable CS0436 // Type conflicts with imported type - intentional for testing
    [MSBuildMultiThreadableTask]
#pragma warning restore CS0436
    public class PidLoggingMultiThreadableTestTask : Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, $"PidLoggingMultiThreadableTestTask executed in PID={Process.GetCurrentProcess().Id}");
            return true;
        }
    }

    #endregion
}

// Test task in the NuGet.Build.Tasks namespace to simulate the real RestoreTask for routing tests.
namespace NuGet.Build.Tasks
{
    /// <summary>
    /// Simulates the NuGet RestoreTask for testing task routing workaround.
    /// Has the same full name (NuGet.Build.Tasks.RestoreTask) that TaskRouter checks.
    /// </summary>
    public class RestoreTask : Task
    {
        public override bool Execute()
        {
            // Include the OS PID so tests can verify each invocation runs in a fresh
            // TaskHost process
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
