// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            var logger = new MockLogger(_output);

            // Act
            BuildResult result = BuildSingleTaskProject("NonEnlightenedTestTask", "NonEnlightenedTaskProject.proj", multiThreaded: true, logger);

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
            var logger = new MockLogger(_output);

            // Act
            BuildResult result = BuildSingleTaskProject("InterfaceTestTask", "InterfaceTaskProject.proj", multiThreaded: true, logger);

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
            var logger = new MockLogger(_output);

            // Act
            BuildResult result = BuildSingleTaskProject("AttributeTestTask", "AttributeTaskProject.proj", multiThreaded: true, logger);

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
            var logger = new MockLogger(_output);

            // Act
            BuildResult result = BuildSingleTaskProject("NonEnlightenedTestTask", "NonEnlightenedTaskSingleThreaded.proj", multiThreaded: false, logger);

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
            var logger = new MockLogger(_output);

            // Act
            BuildResult result = BuildSingleTaskProject("InterfaceTestTask", "InterfaceTaskSingleThreaded.proj", multiThreaded: false, logger);

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

            var logger = new MockLogger(_output);

            // Act
            BuildResult result = BuildProject("MixedTasksProject.proj", projectContent, multiThreaded: true, logger);

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

            var logger = new MockLogger(_output);

            // Act
            BuildResult result = BuildProject("ExplicitTaskHostFactory.proj", projectContent, multiThreaded: true, logger);

            // Assert
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Task should use TaskHost because TaskHostFactory was explicitly requested
            // This overrides the normal routing logic which would run attribute tasks in-process
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "AttributeTestTask");

            // Verify task executed successfully
            logger.FullLog.ShouldContain("TaskWithAttribute executed");
        }

        [Fact]
        public void ExtendedBuildError_RoutedThroughTaskHost_PreservesStructuredData()
        {
            var logger = new MockLogger(_output);

            BuildResult result = BuildSingleTaskProject("ExtendedBuildErrorTestTask", "ExtendedBuildErrorProject.proj", multiThreaded: true, logger);

            result.ShouldHaveFailed();
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "ExtendedBuildErrorTestTask");
            logger.Errors.Count.ShouldBe(1);

            ExtendedBuildErrorEventArgs error = logger.Errors[0].ShouldBeOfType<ExtendedBuildErrorEventArgs>();
            error.Code.ShouldBe("TEST0001");
            error.ExtendedType.ShouldBe("cpp");
            error.ExtendedData.ShouldBe("""{"tool":"cl.exe"}""");
            error.ExtendedMetadata.ShouldNotBeNull();
            error.ExtendedMetadata["source"].ShouldBe("structured-output");
        }

        /// <summary>
        /// Companion to <see cref="ExtendedBuildError_RoutedThroughTaskHost_PreservesStructuredData"/>: the remaining
        /// event kinds that <c>TaskHostTask</c> routes explicitly (critical messages, extended warnings, extended
        /// messages, extended critical messages and extended custom events) must also reach the parent logger as their
        /// concrete types with their structured payload intact.
        /// </summary>
        [Fact]
        public void NonErrorExtendedEvents_RoutedThroughTaskHost_PreserveStructuredData()
        {
            var logger = new MockLogger(_output) { Verbosity = LoggerVerbosity.Diagnostic };

            BuildResult result = BuildSingleTaskProject("ExtendedEventsTestTask", "ExtendedEventsProject.proj", multiThreaded: true, logger);

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "ExtendedEventsTestTask");
            logger.Errors.ShouldBeEmpty();

            // LoggingEventType.CriticalBuildMessage
            CriticalBuildMessageEventArgs criticalMessage = logger.AllBuildEvents
                .Where(e => e.GetType() == typeof(CriticalBuildMessageEventArgs))
                .ShouldHaveSingleItem()
                .ShouldBeOfType<CriticalBuildMessageEventArgs>();
            criticalMessage.Code.ShouldBe("TEST0002");
            criticalMessage.Message.ShouldBe("Critical message");

            // LoggingEventType.ExtendedBuildWarningEvent
            ExtendedBuildWarningEventArgs warning = logger.Warnings.ShouldHaveSingleItem().ShouldBeOfType<ExtendedBuildWarningEventArgs>();
            warning.Code.ShouldBe("TEST0003");
            warning.ExtendedType.ShouldBe("cpp");
            warning.ExtendedData.ShouldBe("""{"tool":"cl.exe"}""");
            warning.ExtendedMetadata.ShouldNotBeNull();
            warning.ExtendedMetadata["source"].ShouldBe("structured-output");

            // LoggingEventType.ExtendedBuildMessageEvent
            ExtendedBuildMessageEventArgs message = logger.AllBuildEvents
                .OfType<ExtendedBuildMessageEventArgs>()
                .ShouldHaveSingleItem();
            message.Code.ShouldBe("TEST0004");
            message.ExtendedType.ShouldBe("cpp");
            message.ExtendedData.ShouldBe("""{"tool":"cl.exe"}""");
            message.ExtendedMetadata.ShouldNotBeNull();
            message.ExtendedMetadata["source"].ShouldBe("structured-output");

            // LoggingEventType.ExtendedCriticalBuildMessageEvent
            ExtendedCriticalBuildMessageEventArgs extendedCriticalMessage = logger.AllBuildEvents
                .OfType<ExtendedCriticalBuildMessageEventArgs>()
                .ShouldHaveSingleItem();
            extendedCriticalMessage.Code.ShouldBe("TEST0005");
            extendedCriticalMessage.ExtendedType.ShouldBe("cpp");
            extendedCriticalMessage.ExtendedData.ShouldBe("""{"tool":"cl.exe"}""");
            extendedCriticalMessage.ExtendedMetadata.ShouldNotBeNull();
            extendedCriticalMessage.ExtendedMetadata["source"].ShouldBe("structured-output");

            // LoggingEventType.ExtendedCustomEvent
            ExtendedCustomBuildEventArgs customEvent = logger.AllBuildEvents
                .OfType<ExtendedCustomBuildEventArgs>()
                .ShouldHaveSingleItem();
            customEvent.Message.ShouldBe("Structured custom event");
            customEvent.ExtendedType.ShouldBe("cpp");
            customEvent.ExtendedData.ShouldBe("""{"tool":"cl.exe"}""");
            customEvent.ExtendedMetadata.ShouldNotBeNull();
            customEvent.ExtendedMetadata["source"].ShouldBe("structured-output");
        }

        /// <summary>
        /// Telemetry raised by a task through <see cref="IBuildEngine5.LogTelemetry"/> must survive the TaskHost
        /// round-trip. A task can only ever supply an event name and a property bag, so forwarding those two values
        /// reproduces exactly what the same task would have produced in-process.
        /// </summary>
        [Fact]
        public void Telemetry_RoutedThroughTaskHost_ReachesLoggers()
        {
            // MockLogger cannot observe telemetry: EventSourceSink routes TelemetryEventArgs to TelemetryLogged only,
            // never to AnyEventRaised. A dedicated IEventSource2 subscriber is required.
            var logger = new MockLogger(_output);
            var telemetryLogger = new TelemetryCapturingLogger();

            BuildResult result = BuildSingleTaskProject("TelemetryTestTask", "TelemetryProject.proj", multiThreaded: true, logger, telemetryLogger);

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "TelemetryTestTask");

            TelemetryEventArgs telemetry = telemetryLogger.TelemetryEvents
                .Where(e => e.EventName == "TaskHostTelemetryEvent")
                .ShouldHaveSingleItem();
            telemetry.Properties["tool"].ShouldBe("cl.exe");
            telemetry.Properties["exitCode"].ShouldBe("0");
        }

        /// <summary>
        /// The switch in <c>TaskHostTask.HandleLoggedMessage</c> is an allow-list, so any event kind without an
        /// explicit case was silently discarded. <see cref="ExternalProjectStartedEventArgs"/> is a framework type
        /// with its own <c>LoggingEventType</c> that a task can log through <see cref="IBuildEngine.LogCustomEvent"/>,
        /// which makes it a representative of that whole class of dropped events. It must now reach the parent logger
        /// as its concrete type by way of the fallback dispatch on the event's base type.
        /// </summary>
        [Fact]
        public void EventKindWithoutExplicitCase_RoutedThroughTaskHost_ReachesLoggers()
        {
            var logger = new MockLogger(_output) { Verbosity = LoggerVerbosity.Diagnostic };

            BuildResult result = BuildSingleTaskProject("UnlistedEventTestTask", "UnlistedEventProject.proj", multiThreaded: true, logger);

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "UnlistedEventTestTask");
            logger.Errors.ShouldBeEmpty();

            ExternalProjectStartedEventArgs externalProjectStarted = logger.AllBuildEvents
                .OfType<ExternalProjectStartedEventArgs>()
                .ShouldHaveSingleItem();
            externalProjectStarted.Message.ShouldBe("External project started");
            externalProjectStarted.ProjectFile.ShouldBe("external.proj");
            externalProjectStarted.TargetNames.ShouldBe("Build");
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

            var logger = new MockLogger(_output);

            // Act
            BuildResult result = BuildProject($"NoAssemblyLocationMismatch_{multiThreaded}.proj", projectContent, multiThreaded, logger);

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

        private string CreateTestProject(string taskName)
        {
            return $@"
<Project>
    <UsingTask TaskName=""{taskName}"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    
    <Target Name=""TestTarget"">
        <{taskName} />
    </Target>
</Project>";
        }

        /// <summary>
        /// Writes <paramref name="projectContent"/> into the test folder and builds its <c>TestTarget</c>.
        /// Loggers are supplied by the caller so that each test can assert against them afterwards.
        /// </summary>
        private BuildResult BuildProject(string projectFileName, string projectContent, bool multiThreaded, params ILogger[] loggers)
        {
            string projectFile = Path.Combine(_testProjectsDir, projectFileName);
            File.WriteAllText(projectFile, projectContent);

            var buildParameters = new BuildParameters
            {
                MultiThreaded = multiThreaded,
                Loggers = loggers,
                DisableInProcNode = false,
                EnableNodeReuse = false
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string>(),
                null,
                ["TestTarget"],
                null);

            return BuildManager.DefaultBuildManager.Build(buildParameters, buildRequestData);
        }

        /// <summary>
        /// Builds a project whose single target invokes <paramref name="taskName"/> once.
        /// </summary>
        private BuildResult BuildSingleTaskProject(string taskName, string projectFileName, bool multiThreaded = true, params ILogger[] loggers)
            => BuildProject(projectFileName, CreateTestProject(taskName), multiThreaded, loggers);
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

    /// <summary>
    /// Captures telemetry events, which <see cref="MockLogger"/> cannot observe because
    /// <c>EventSourceSink</c> routes <see cref="TelemetryEventArgs"/> exclusively to
    /// <see cref="IEventSource2.TelemetryLogged"/>.
    /// </summary>
    internal sealed class TelemetryCapturingLogger : ILogger
    {
        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

        public string Parameters { get; set; }

        public List<TelemetryEventArgs> TelemetryEvents { get; } = [];

        public void Initialize(IEventSource eventSource)
        {
            if (eventSource is IEventSource2 eventSource2)
            {
                eventSource2.TelemetryLogged += (sender, e) => TelemetryEvents.Add(e);
            }
        }

        public void Shutdown()
        {
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
    /// Logs a framework event kind that <c>TaskHostTask.HandleLoggedMessage</c> has no explicit case for, so the
    /// test can assert that the fallback dispatch forwards it instead of discarding it.
    /// </summary>
    public class UnlistedEventTestTask : Task
    {
        public override bool Execute()
        {
            BuildEngine.LogCustomEvent(new ExternalProjectStartedEventArgs(
                message: "External project started",
                helpKeyword: null,
                senderName: nameof(UnlistedEventTestTask),
                projectFile: "external.proj",
                targetNames: "Build"));

            return true;
        }
    }

    /// <summary>
    /// Logs telemetry the way a real task does, through <see cref="TaskLoggingHelper.LogTelemetry"/>.
    /// </summary>
    public class TelemetryTestTask : Task
    {
        public override bool Execute()
        {
            Log.LogTelemetry("TaskHostTelemetryEvent", new Dictionary<string, string>
            {
                ["tool"] = "cl.exe",
                ["exitCode"] = "0"
            });

            return true;
        }
    }

    public class ExtendedBuildErrorTestTask : Task    {
        public override bool Execute()
        {
            BuildEngine.LogErrorEvent(new ExtendedBuildErrorEventArgs(
                "cpp",
                subcategory: null,
                code: "TEST0001",
                file: "source.cpp",
                lineNumber: 1,
                columnNumber: 2,
                endLineNumber: 1,
                endColumnNumber: 3,
                message: "Structured compiler error",
                helpKeyword: null,
                senderName: nameof(ExtendedBuildErrorTestTask))
            {
                ExtendedData = """{"tool":"cl.exe"}""",
                ExtendedMetadata = new Dictionary<string, string>
                {
                    ["source"] = "structured-output"
                }
            });

            return false;
        }
    }

    /// <summary>
    /// Logs one instance of each non-error event kind that <c>TaskHostTask</c> routes explicitly, so the test can
    /// assert that each survives the TaskHost round-trip as its concrete type.
    /// </summary>
    public class ExtendedEventsTestTask : Task
    {
        private const string ExtendedDataJson = """{"tool":"cl.exe"}""";

        public override bool Execute()
        {
            Log.LogCriticalMessage(
                subcategory: null,
                code: "TEST0002",
                helpKeyword: null,
                file: "source.cpp",
                lineNumber: 1,
                columnNumber: 2,
                endLineNumber: 1,
                endColumnNumber: 3,
                message: "Critical message");

            BuildEngine.LogWarningEvent(new ExtendedBuildWarningEventArgs(
                "cpp",
                subcategory: null,
                code: "TEST0003",
                file: "source.cpp",
                lineNumber: 1,
                columnNumber: 2,
                endLineNumber: 1,
                endColumnNumber: 3,
                message: "Structured compiler warning",
                helpKeyword: null,
                senderName: nameof(ExtendedEventsTestTask))
            {
                ExtendedData = ExtendedDataJson,
                ExtendedMetadata = CreateMetadata()
            });

            BuildEngine.LogMessageEvent(new ExtendedBuildMessageEventArgs(
                "cpp",
                subcategory: null,
                code: "TEST0004",
                file: "source.cpp",
                lineNumber: 1,
                columnNumber: 2,
                endLineNumber: 1,
                endColumnNumber: 3,
                message: "Structured compiler message",
                helpKeyword: null,
                senderName: nameof(ExtendedEventsTestTask),
                importance: MessageImportance.High)
            {
                ExtendedData = ExtendedDataJson,
                ExtendedMetadata = CreateMetadata()
            });

            BuildEngine.LogMessageEvent(new ExtendedCriticalBuildMessageEventArgs(
                "cpp",
                subcategory: null,
                code: "TEST0005",
                file: "source.cpp",
                lineNumber: 1,
                columnNumber: 2,
                endLineNumber: 1,
                endColumnNumber: 3,
                message: "Structured critical message",
                helpKeyword: null,
                senderName: nameof(ExtendedEventsTestTask))
            {
                ExtendedData = ExtendedDataJson,
                ExtendedMetadata = CreateMetadata()
            });

            BuildEngine.LogCustomEvent(new ExtendedCustomBuildEventArgs(
                "cpp",
                message: "Structured custom event",
                helpKeyword: null,
                senderName: nameof(ExtendedEventsTestTask))
            {
                ExtendedData = ExtendedDataJson,
                ExtendedMetadata = CreateMetadata()
            });

            return true;
        }

        private static Dictionary<string, string> CreateMetadata() => new()
        {
            ["source"] = "structured-output"
        };
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
