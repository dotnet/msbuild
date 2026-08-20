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
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NonEnlightenedTask_RunsInTaskHost_InMultiThreadedMode(bool disableInProcNode)
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
                DisableInProcNode = disableInProcNode,
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

        [Theory]
        [InlineData("worker", true, true, true, false, false)]
        [InlineData("devenv", true, false, true, true, false)]
        [InlineData("off", false, false, false, true, true)]
        public void VisualStudioModeSelectsBuildParametersAndTaskTopology(
            string mode,
            bool expectedMultiThreaded,
            bool expectedDisableInProcNode,
            bool expectedTaskHost,
            bool expectedAttributeTaskInCurrentProcess,
            bool expectedLegacyTaskInCurrentProcess)
        {
            string projectContent = $"""
                <Project>
                  <UsingTask TaskName="NonEnlightenedTestTask" AssemblyFile="{Assembly.GetExecutingAssembly().Location}" />
                  <UsingTask TaskName="AttributeTestTask" AssemblyFile="{Assembly.GetExecutingAssembly().Location}" />
                  <Target Name="DesignTimeTarget">
                    <NonEnlightenedTestTask />
                    <AttributeTestTask />
                  </Target>
                </Project>
                """;

            string projectFile = Path.Combine(_testProjectsDir, $"VisualStudioDesignTime_{mode}.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var callerParameters = new BuildParameters
            {
                DisableInProcNode = false,
                EnableNodeReuse = false,
                Loggers = [logger],
                MaxNodeCount = 2,
                MultiThreaded = false,
                SaveOperatingEnvironment = true,
            };
            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>
                {
                    [DesignTimeProperties.DesignTimeBuild] = "true",
                },
                null,
                ["DesignTimeTarget"],
                null);

            BuildEnvironment currentBuildEnvironment = BuildEnvironmentHelper.Instance;
            _env.SetEnvironmentVariable(Traits.VisualStudioMultiThreadedModeEnvVarName, mode);
            _env.SetEnvironmentVariable(Traits.DisableVisualStudioMultiThreadedEnvVarName, null);

            try
            {
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(
                    new BuildEnvironment(
                        currentBuildEnvironment.Mode,
                        currentBuildEnvironment.CurrentMSBuildExePath,
                        currentBuildEnvironment.RunningTests,
                        currentBuildEnvironment.RunningInMSBuildExe,
                        runningInVisualStudio: true,
                        visualStudioPath: currentBuildEnvironment.VisualStudioInstallRootDirectory));

                using var buildManager = new BuildManager();
                buildManager.BeginBuild(callerParameters);
                BuildResult result;
                try
                {
                    BuildParameters effectiveParameters = ((IBuildComponentHost)buildManager).BuildParameters;
                    effectiveParameters.MultiThreaded.ShouldBe(expectedMultiThreaded);
                    effectiveParameters.DisableInProcNode.ShouldBe(expectedDisableInProcNode);
                    effectiveParameters.EnableNodeReuse.ShouldBeFalse();
                    effectiveParameters.SaveOperatingEnvironment.ShouldBe(mode != "devenv");

                    callerParameters.MultiThreaded.ShouldBeFalse();
                    callerParameters.DisableInProcNode.ShouldBeFalse();
                    callerParameters.EnableNodeReuse.ShouldBeFalse();
                    callerParameters.SaveOperatingEnvironment.ShouldBeTrue();

                    result = buildManager.BuildRequest(buildRequestData);
                }
                finally
                {
                    buildManager.EndBuild();
                }

                result.ShouldHaveSucceeded();
            }
            finally
            {
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(currentBuildEnvironment);
            }

            if (expectedTaskHost)
            {
                TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "NonEnlightenedTestTask");
            }
            else
            {
                TaskRouterTestHelper.AssertTaskRanInProcess(logger, "NonEnlightenedTestTask");
            }

            int currentProcessId = Process.GetCurrentProcess().Id;
            (GetLoggedProcessId(logger, "ROUTING_MT_TASK_PID") == currentProcessId)
                .ShouldBe(expectedAttributeTaskInCurrentProcess);
            (GetLoggedProcessId(logger, "ROUTING_LEGACY_TASK_PID") == currentProcessId)
                .ShouldBe(expectedLegacyTaskInCurrentProcess);
            logger.WarningCount.ShouldBe(0);
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
        /// Regression test: a task routed to a TaskHost must not log <c>TaskAssemblyLocationMismatch</c>.
        /// <c>TaskHostTask</c> is only an in-proc proxy for a task that is loaded in a separate process, so
        /// its own assembly location (Microsoft.Build.dll) can never match the resolved task assembly path
        /// and the diagnostic carries no information. In multi-threaded mode this is the common path, so a
        /// stale check produced one spurious message per task execution.
        /// The registered path deliberately contains a redundant "." segment so that it is not byte-identical
        /// to <c>Assembly.Location</c> - the same normalization difference real builds hit.
        /// </summary>
        [Theory]
        [InlineData(true, "")] // Routed to the sidecar TaskHost because multi-threaded mode is on.
        [InlineData(false, @" TaskFactory=""TaskHostFactory""")] // Routed to a TaskHost by explicit request.
        public void TaskHostRoutedTask_DoesNotLogAssemblyLocationMismatch(bool multiThreaded, string taskFactoryAttribute)
        {
            // Arrange
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string nonNormalizedAssemblyPath = Path.Combine(Path.GetDirectoryName(assemblyPath), ".", Path.GetFileName(assemblyPath));

            string projectContent = $@"
<Project>
    <UsingTask TaskName=""NonEnlightenedTestTask"" AssemblyFile=""{nonNormalizedAssemblyPath}""{taskFactoryAttribute} />

    <Target Name=""TestTarget"">
        <NonEnlightenedTestTask />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, $"NoAssemblyLocationMismatch_{multiThreaded}.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = multiThreaded,
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
            var result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequestData);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "NonEnlightenedTestTask");
            logger.FullLog.ShouldContain("NonEnlightenedTask executed");

            // Assert on the localized text preceding the first format argument so the test is locale-safe
            // and does not depend on the exact paths that would have been reported.
            const string ArgumentSentinel = "<<arg>>";
            string mismatchMessage = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
                "TaskAssemblyLocationMismatch",
                ArgumentSentinel,
                ArgumentSentinel);
            string mismatchMessagePrefix = mismatchMessage.Substring(0, mismatchMessage.IndexOf(ArgumentSentinel, StringComparison.Ordinal));

            logger.FullLog.ShouldNotContain(mismatchMessagePrefix);
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

        private static int GetLoggedProcessId(MockLogger logger, string prefix)
        {
            Match match = Regex.Match(logger.FullLog, $@"{prefix}=(\d+)");
            match.Success.ShouldBeTrue($"Expected {prefix} in the build log.");
            return int.Parse(match.Groups[1].Value);
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
            Log.LogMessage(MessageImportance.High, $"ROUTING_LEGACY_TASK_PID={Process.GetCurrentProcess().Id}");
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
        [Output]
        public int ProcessId { get; set; }

        public override bool Execute()
        {
            ProcessId = Process.GetCurrentProcess().Id;
            Log.LogMessage(MessageImportance.High, "TaskWithAttribute executed");
            Log.LogMessage(MessageImportance.High, $"ROUTING_MT_TASK_PID={ProcessId}");
            return true;
        }
    }

    #endregion
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
