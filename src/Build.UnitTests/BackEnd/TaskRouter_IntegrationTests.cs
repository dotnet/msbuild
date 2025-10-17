// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Integration tests for task routing in multi-threaded mode.
    /// Tests verify that tasks with IMultiThreadableTask or MSBuildMultiThreadableTaskAttribute
    /// run in-process, while tasks without these indicators run in TaskHost for isolation.
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
        /// Verifies that a task with IMultiThreadableTask interface runs in-process
        /// (not in TaskHost) when MultiThreaded mode is enabled.
        /// </summary>
        [Fact]
        public void TaskWithInterface_RunsInProcess_InMultiThreadedMode()
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

            // Verify task was NOT launched in TaskHost (runs in-process)
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "InterfaceTestTask");

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
        /// Verifies that a task with interface doesn't use TaskHost in single-threaded mode.
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

            // NonEnlightenedTask should use TaskHost
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "NonEnlightenedTestTask");

            // Interface and Attribute tasks should NOT use TaskHost
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "InterfaceTestTask");
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "AttributeTestTask");

            // All tasks should execute successfully
            logger.FullLog.ShouldContain("NonEnlightenedTask executed");
            logger.FullLog.ShouldContain("TaskWithInterface executed");
            logger.FullLog.ShouldContain("TaskWithAttribute executed");
        }

        /// <summary>
        /// Verifies that explicit TaskHostFactory request overrides routing logic,
        /// forcing tasks to run in TaskHost even if they would normally run in-process.
        /// </summary>
        [Fact]
        public void ExplicitTaskHostFactory_OverridesRoutingLogic()
        {
            // Arrange - Use a thread-safe task but explicitly request TaskHostFactory
            string projectContent = $@"
<Project>
    <UsingTask TaskName=""InterfaceTestTask"" 
               AssemblyFile=""{Assembly.GetExecutingAssembly().Location}""
               TaskFactory=""TaskHostFactory"" />
    
    <Target Name=""TestTarget"">
        <InterfaceTestTask />
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
            // This overrides the normal routing logic which would run this in-process
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "InterfaceTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("TaskWithInterface executed");
        }

        /// <summary>
        /// Verifies that tasks inheriting IMultiThreadableTask from an unmarked external base class
        /// run in-process (inheritable semantics by default).
        /// </summary>
        [Fact]
        public void InheritedInterface_RunsInProcess_InMultiThreadedMode()
        {
            // Arrange
            string projectContent = CreateTestProject(
                taskName: "InheritedInterfaceTestTask",
                taskClass: "InheritedInterfaceTask");

            string projectFile = Path.Combine(_testProjectsDir, "InheritedInterfaceProject.proj");
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

            // Task should run in-process because it inherits from unmarked external base class
            // (inheritable semantics)
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "InheritedInterfaceTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("InheritedInterfaceTask executed");
        }

        /// <summary>
        /// Verifies that tasks inheriting IMultiThreadableTask from an InternalMSBuildTaskAttribute-marked
        /// base class (MSBuild repo tasks) are routed to TaskHost for backward compatibility
        /// (non-inheritable semantics).
        /// </summary>
        [Fact]
        public void InheritedFromInternalBase_RunsInTaskHost_InMultiThreadedMode()
        {
            // Arrange
            string projectContent = CreateTestProject(
                taskName: "InheritedFromInternalBaseTestTask",
                taskClass: "InheritedFromInternalBaseTask");

            string projectFile = Path.Combine(_testProjectsDir, "InheritedFromInternalBaseProject.proj");
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

            // Task should use TaskHost because base class has InternalMSBuildTaskAttribute
            // (non-inheritable semantics for backward compatibility)
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "InheritedFromInternalBaseTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("InheritedFromInternalBaseTask executed");
        }

        /// <summary>
        /// Verifies that multi-level inheritance from unmarked external base class maintains
        /// inheritable semantics - tasks should run in-process.
        /// </summary>
        [Fact]
        public void MultiLevelInheritanceFromUnmarkedBase_RunsInProcess_InMultiThreadedMode()
        {
            // Arrange
            string projectContent = CreateTestProject(
                taskName: "MultiLevelUnmarkedTestTask",
                taskClass: "MultiLevelUnmarkedTask");

            string projectFile = Path.Combine(_testProjectsDir, "MultiLevelUnmarkedProject.proj");
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

            // Task should run in-process - inheritable semantics through multiple levels
            TaskRouterTestHelper.AssertTaskRanInProcess(logger, "MultiLevelUnmarkedTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("MultiLevelUnmarkedTask executed");
        }

        /// <summary>
        /// Verifies that multi-level inheritance from InternalMSBuildTaskAttribute-marked base
        /// applies non-inheritable semantics - tasks should run in TaskHost even if the attribute
        /// is on a grandparent class.
        /// </summary>
        [Fact]
        public void MultiLevelInheritanceFromMarkedBase_RunsInTaskHost_InMultiThreadedMode()
        {
            // Arrange
            string projectContent = CreateTestProject(
                taskName: "MultiLevelMarkedTestTask",
                taskClass: "MultiLevelMarkedTask");

            string projectFile = Path.Combine(_testProjectsDir, "MultiLevelMarkedProject.proj");
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

            // Task should use TaskHost - non-inheritable semantics from marked grandparent
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "MultiLevelMarkedTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("MultiLevelMarkedTask executed");
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
    /// Base task class that implements IMultiThreadableTask.
    /// This simulates an external base class (not from MSBuild repo).
    /// </summary>
    public class BaseMultiThreadableTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "BaseMultiThreadableTask executed");
            return true;
        }
    }

    /// <summary>
    /// Base task class that implements IMultiThreadableTask and is marked with InternalMSBuildTaskAttribute.
    /// This simulates a task from the MSBuild repository that needs non-inheritable semantics.
    /// </summary>
    [InternalMSBuildTask]
    public class InternalBaseMultiThreadableTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "InternalBaseMultiThreadableTask executed");
            return true;
        }
    }

    /// <summary>
    /// Task that inherits from BaseMultiThreadableTask (unmarked external base).
    /// Should run IN-PROCESS in multi-threaded mode because of inheritable semantics.
    /// </summary>
    public class InheritedInterfaceTestTask : BaseMultiThreadableTask
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "InheritedInterfaceTask executed");
            return true;
        }
    }

    /// <summary>
    /// Task that inherits from InternalBaseMultiThreadableTask (marked MSBuild repo base).
    /// Should run in TaskHost in multi-threaded mode because of non-inheritable semantics
    /// for backward compatibility.
    /// </summary>
    public class InheritedFromInternalBaseTestTask : InternalBaseMultiThreadableTask
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "InheritedFromInternalBaseTask executed");
            return true;
        }
    }

    /// <summary>
    /// Intermediate task class for multi-level inheritance testing (unmarked).
    /// Inherits from BaseMultiThreadableTask.
    /// </summary>
    public class MiddleUnmarkedTask : BaseMultiThreadableTask
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "MiddleUnmarkedTask executed");
            return true;
        }
    }

    /// <summary>
    /// Task that inherits from MiddleUnmarkedTask (multi-level from unmarked base).
    /// Should run in-process because of inheritable semantics through the chain.
    /// </summary>
    public class MultiLevelUnmarkedTestTask : MiddleUnmarkedTask
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "MultiLevelUnmarkedTask executed");
            return true;
        }
    }

    /// <summary>
    /// Intermediate task class for multi-level inheritance testing (from marked base).
    /// Inherits from InternalBaseMultiThreadableTask.
    /// </summary>
    public class MiddleMarkedTask : InternalBaseMultiThreadableTask
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "MiddleMarkedTask executed");
            return true;
        }
    }

    /// <summary>
    /// Task that inherits from MiddleMarkedTask (multi-level from marked base).
    /// Should run in TaskHost because InternalMSBuildTaskAttribute appears in the hierarchy.
    /// </summary>
    public class MultiLevelMarkedTestTask : MiddleMarkedTask
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "MultiLevelMarkedTask executed");
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

// Custom attribute definition in Microsoft.Build.Framework namespace to match what TaskRouter expects
// TaskRouter looks for attributes with FullName = "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute"
// Since the real attribute is internal in Framework, we define our own test version here
namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Test attribute to mark tasks as safe for multi-threaded execution.
    /// This is a test copy in this test assembly that will be recognized
    /// by name-based attribute detection in TaskRouter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class MSBuildMultiThreadableTaskAttribute : Attribute
    {
    }
}
