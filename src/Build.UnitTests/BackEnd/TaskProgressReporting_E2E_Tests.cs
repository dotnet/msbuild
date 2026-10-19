// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// End-to-end tests validating that task progress reporting (<see cref="ITaskProgressReporter"/>) round-trips
    /// through a real build, both when the reporting task runs in-process and when it is routed through an
    /// explicitly requested <c>TaskHostFactory</c> (a separate <c>MSBuild.exe</c>/<c>dll</c> process, wired via
    /// <see cref="Microsoft.Build.BackEnd.TaskProgressManager"/> shared code and forwarded across the node
    /// connection by <c>OutOfProcTaskHostNode</c>). Both routes must deliver begin/update/end records to the
    /// parent logger with correct sequencing and a correlated, valid <see cref="BuildEventContext"/>.
    /// </summary>
    public class TaskProgressReporting_E2E_Tests
    {
        private readonly ITestOutputHelper _output;

        public TaskProgressReporting_E2E_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ProgressReporting_InProcess_ReachesParentLoggerWithCorrelatedContext()
        {
            var logger = new MockLogger(_output);

            BuildResult result = BuildTaskProject(taskHostFactory: false, logger);

            result.OverallResult.ShouldBe(BuildResultCode.Success);

            AssertTwoOperationsReachedLoggerInOrder(logger);
        }

        [Fact]
        public void ProgressReporting_ThroughExplicitTaskHostFactory_ReachesParentLoggerWithCorrelatedContext()
        {
            var logger = new MockLogger(_output);

            BuildResult result = BuildTaskProject(taskHostFactory: true, logger);

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            logger.FullLog.ShouldContain("external task host");

            AssertTwoOperationsReachedLoggerInOrder(logger);
        }

        [Fact]
        public void ProgressReporting_FromBlockedTaskHost_RetainsItsContextDuringNestedTask()
        {
            var logger = new MockLogger(_output);
            using TestEnvironment env = TestEnvironment.Create(_output);

            string taskAssembly = Assembly.GetExecutingAssembly().Location;
            TransientTestFolder folder = env.CreateFolder();
            string childProject = Path.Combine(folder.Path, "NestedProgressProject.proj");
            string parentProject = Path.Combine(folder.Path, "ParentProgressProject.proj");

            File.WriteAllText(childProject, $"""
                <Project>
                    <UsingTask TaskName="{nameof(NestedProgressChildTask)}" AssemblyFile="{taskAssembly}" TaskFactory="TaskHostFactory" />
                    <Target Name="Build">
                        <{nameof(NestedProgressChildTask)} />
                    </Target>
                </Project>
                """);

            File.WriteAllText(parentProject, $"""
                <Project>
                    <UsingTask TaskName="{nameof(NestedProgressParentTask)}" AssemblyFile="{taskAssembly}" TaskFactory="TaskHostFactory" />
                    <Target Name="Build">
                        <{nameof(NestedProgressParentTask)} ChildProject="$(ChildProject)" />
                    </Target>
                </Project>
                """);

            var buildParameters = new BuildParameters
            {
                MultiThreaded = false,
                Loggers = [logger],
                DisableInProcNode = false,
                EnableNodeReuse = false,
                MaxNodeCount = 1,
            };

            var buildRequestData = new BuildRequestData(
                parentProject,
                new Dictionary<string, string?> { ["ChildProject"] = childProject },
                null,
                ["Build"],
                null);

            BuildResult result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequestData);
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            TaskProgressStartedEventArgs parentStarted = logger.AllBuildEvents
                .OfType<TaskProgressStartedEventArgs>()
                .Single(e => e.Title == "Parent operation");
            TaskProgressStartedEventArgs childStarted = logger.AllBuildEvents
                .OfType<TaskProgressStartedEventArgs>()
                .Single(e => e.Title == "Nested operation");
            parentStarted.BuildEventContext.ShouldNotBeNull();
            childStarted.BuildEventContext.ShouldNotBeNull();
            parentStarted.BuildEventContext.ShouldNotBe(childStarted.BuildEventContext);

            TaskProgressUpdatedEventArgs parentUpdate = logger.AllBuildEvents
                .OfType<TaskProgressUpdatedEventArgs>()
                .Single(e => e.Status == "Waiting for nested task");
            parentUpdate.BuildEventContext.ShouldBe(parentStarted.BuildEventContext);
            logger.AllBuildEvents
                .OfType<TaskProgressFinishedEventArgs>()
                .Single(e => e.OperationId == parentStarted.OperationId)
                .BuildEventContext.ShouldBe(parentStarted.BuildEventContext);

            logger.AllBuildEvents
                .OfType<TaskProgressUpdatedEventArgs>()
                .Single(e => e.OperationId == childStarted.OperationId)
                .BuildEventContext.ShouldBe(childStarted.BuildEventContext);
            logger.AllBuildEvents
                .OfType<TaskProgressFinishedEventArgs>()
                .Single(e => e.OperationId == childStarted.OperationId)
                .BuildEventContext.ShouldBe(childStarted.BuildEventContext);
        }

        [Fact]
        public void ProgressReporting_ThroughWorkerNode_ReachesParentLoggerWithCorrelatedContext()
        {
            var logger = new MockLogger(_output);

            BuildResult result = BuildTaskProject(
                taskHostFactory: false,
                logger,
                useWorkerNode: true);

            result.OverallResult.ShouldBe(BuildResultCode.Success);

            AssertTwoOperationsReachedLoggerInOrder(logger);

            foreach (TaskProgressStartedEventArgs start in logger.AllBuildEvents.OfType<TaskProgressStartedEventArgs>())
            {
                start.BuildEventContext!.NodeId.ShouldNotBe(Scheduler.InProcNodeId);
            }
        }

#if FEATURE_APPDOMAIN
        [WindowsFullFrameworkOnlyFact]
        public void ProgressReporting_ThroughSeparateAppDomain_ReachesParentLoggerWithCorrelatedContext()
        {
            var logger = new MockLogger(_output);

            BuildResult result = BuildTaskProject(
                taskHostFactory: false,
                logger,
                taskName: nameof(AppDomainProgressReportingTestTask));

            result.OverallResult.ShouldBe(BuildResultCode.Success);

            AssertTwoOperationsReachedLoggerInOrder(logger);
        }
#endif

        /// <summary>
        /// <see cref="ProgressReportingTestTask"/> creates two distinct operations. Both routes must keep them
        /// distinct, deliver each one's begin/update/end records in order, and stamp every record for one
        /// operation with the same, valid <see cref="BuildEventContext"/> so the task that produced it can be
        /// identified.
        /// </summary>
        private static void AssertTwoOperationsReachedLoggerInOrder(MockLogger logger)
        {
            List<TaskProgressStartedEventArgs> started = logger.AllBuildEvents.OfType<TaskProgressStartedEventArgs>().ToList();
            List<TaskProgressUpdatedEventArgs> updated = logger.AllBuildEvents.OfType<TaskProgressUpdatedEventArgs>().ToList();
            List<TaskProgressFinishedEventArgs> finished = logger.AllBuildEvents.OfType<TaskProgressFinishedEventArgs>().ToList();

            started.Count.ShouldBe(2);
            updated.Count.ShouldBe(2);
            finished.Count.ShouldBe(2);

            TaskProgressStartedEventArgs downloadStarted = started.Single(s => s.Title == "Downloading package");
            TaskProgressStartedEventArgs extractStarted = started.Single(s => s.Title == "Extracting package");

            downloadStarted.OperationId.ShouldNotBe(extractStarted.OperationId);
            downloadStarted.Unit.ShouldBe(TaskProgressUnit.Bytes);
            extractStarted.Unit.ShouldBe(TaskProgressUnit.Items);

            downloadStarted.BuildEventContext.ShouldNotBeNull();
            downloadStarted.BuildEventContext!.TaskId.ShouldNotBe(BuildEventContext.InvalidTaskId);

            TaskProgressUpdatedEventArgs downloadUpdate = updated.Single(u => u.OperationId == downloadStarted.OperationId);
            TaskProgressFinishedEventArgs downloadFinished = finished.Single(f => f.OperationId == downloadStarted.OperationId);

            downloadUpdate.Completed.ShouldBe(50);
            downloadUpdate.Total.ShouldBe(100);
            downloadUpdate.Status.ShouldBe("Downloading");
            downloadUpdate.Sequence.ShouldBe(1);
            downloadUpdate.BuildEventContext.ShouldBe(downloadStarted.BuildEventContext);

            downloadFinished.Outcome.ShouldBe(TaskProgressOutcome.Completed);
            downloadFinished.Completed.ShouldBe(100);
            downloadFinished.Total.ShouldBe(100);
            downloadFinished.Summary.ShouldBe("Downloaded");
            downloadFinished.Sequence.ShouldBe(3);
            downloadFinished.BuildEventContext.ShouldBe(downloadStarted.BuildEventContext);

            TaskProgressUpdatedEventArgs extractUpdate = updated.Single(u => u.OperationId == extractStarted.OperationId);
            TaskProgressFinishedEventArgs extractFinished = finished.Single(f => f.OperationId == extractStarted.OperationId);

            extractUpdate.Completed.ShouldBe(3);
            extractUpdate.Total.ShouldBe(10);
            extractUpdate.Status.ShouldBe("Extracting");

            extractFinished.Outcome.ShouldBe(TaskProgressOutcome.Completed);
            extractFinished.Completed.ShouldBe(10);
            extractFinished.Total.ShouldBe(10);
            extractFinished.Summary.ShouldBe("Extracted");
            extractFinished.BuildEventContext.ShouldBe(extractStarted.BuildEventContext);
        }

        private BuildResult BuildTaskProject(
            bool taskHostFactory,
            MockLogger logger,
            bool useWorkerNode = false,
            string taskName = nameof(ProgressReportingTestTask))
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string taskFactoryAttribute = taskHostFactory ? @" TaskFactory=""TaskHostFactory""" : string.Empty;
            string projectContent = $@"
<Project>
    <UsingTask TaskName=""{taskName}"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}""{taskFactoryAttribute} />

    <Target Name=""TestTarget"">
        <{taskName} />
    </Target>
</Project>";

            TransientTestFolder folder = env.CreateFolder();
            string projectFile = Path.Combine(folder.Path, "ProgressReportingProject.proj");
            File.WriteAllText(projectFile, projectContent);

            var buildParameters = new BuildParameters
            {
                MultiThreaded = false,
                Loggers = [logger],
                DisableInProcNode = false,
                EnableNodeReuse = false,
                MaxNodeCount = useWorkerNode ? 2 : 1,
            };

            if (useWorkerNode)
            {
                buildParameters.DisableInProcNode = true;
            }

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string?>(),
                null,
                ["TestTarget"],
                null);

            return BuildManager.DefaultBuildManager.Build(buildParameters, buildRequestData);
        }
    }

    /// <summary>
    /// Creates two progress-reporting operations through <see cref="IBuildEngine10.EngineServices"/>. Each reports
    /// an initial update (always forwarded, since the first report on a fresh reporter bypasses the forwarding
    /// throttle) followed immediately by a second, fully-complete update (throttled, so it is tracked but not
    /// forwarded on its own) before completing. This keeps forwarded record counts deterministic while still
    /// exercising the fact that the terminal record always reports the operation's latest known values.
    /// </summary>
    public class ProgressReportingTestTask : Task
    {
        public override bool Execute()
        {
            EngineServices services = ((IBuildEngine10)BuildEngine).EngineServices;

            using (ITaskProgressReporter download = services.CreateTaskProgressReporter("Downloading package", TaskProgressUnit.Bytes))
            {
                download.Report(new TaskProgressUpdate(50, 100, "Downloading"));
                download.Report(new TaskProgressUpdate(100, 100, "Download complete"));
                download.Complete("Downloaded");
            }

            using (ITaskProgressReporter extract = services.CreateTaskProgressReporter("Extracting package", TaskProgressUnit.Items))
            {
                extract.Report(new TaskProgressUpdate(3, 10, "Extracting"));
                extract.Report(new TaskProgressUpdate(10, 10, "Extraction complete"));
                extract.Complete("Extracted");
            }

            return true;
        }
    }

    public sealed class NestedProgressParentTask : Task
    {
        public string ChildProject { get; set; } = string.Empty;

        public override bool Execute()
        {
            EngineServices services = ((IBuildEngine10)BuildEngine).EngineServices;
            using ITaskProgressReporter reporter = services.CreateTaskProgressReporter("Parent operation");
            reporter.Report(new TaskProgressUpdate(0, 1, "Waiting"));
            reporter.SetStatusProvider(static () => "Waiting for nested task");

            bool success = BuildEngine.BuildProjectFile(ChildProject, ["Build"], new Hashtable(), new Hashtable());
            reporter.Complete();
            return success;
        }
    }

    public sealed class NestedProgressChildTask : Task
    {
        public override bool Execute()
        {
            Thread.Sleep(1000);

            EngineServices services = ((IBuildEngine10)BuildEngine).EngineServices;
            using ITaskProgressReporter reporter = services.CreateTaskProgressReporter("Nested operation");
            reporter.Report(new TaskProgressUpdate(1, 1, "Nested work"));
            reporter.Complete();
            return true;
        }
    }

#if FEATURE_APPDOMAIN
    [LoadInSeparateAppDomain]
    public sealed class AppDomainProgressReportingTestTask : AppDomainIsolatedTask
    {
        public override bool Execute()
        {
            EngineServices services = ((IBuildEngine10)BuildEngine).EngineServices;

            using (ITaskProgressReporter download = services.CreateTaskProgressReporter("Downloading package", TaskProgressUnit.Bytes))
            {
                download.Report(new TaskProgressUpdate(50, 100, "Downloading"));
                download.Report(new TaskProgressUpdate(100, 100, "Download complete"));
                download.Complete("Downloaded");
            }

            using (ITaskProgressReporter extract = services.CreateTaskProgressReporter("Extracting package", TaskProgressUnit.Items))
            {
                extract.Report(new TaskProgressUpdate(3, 10, "Extracting"));
                extract.Report(new TaskProgressUpdate(10, 10, "Extraction complete"));
                extract.Complete("Extracted");
            }

            return true;
        }
    }
#endif
}
