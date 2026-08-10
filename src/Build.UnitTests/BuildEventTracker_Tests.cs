// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.CommandLine.UnitTests;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class BuildEventTracker_Tests
{
    [Fact]
    public void CorrelatesProjectScopedEventsWithEvaluationData()
    {
        var eventSource = new MockBuildEventSink(0);
        var tracker = new BuildEventTracker();
        var correlatedProjects = new List<BuildEventTracker.ProjectSnapshot?>();
        BuildEventTracker.ProjectSnapshot? startedProject = null;

        tracker.ProjectStartedTracked += project => startedProject = project;
        tracker.ProjectFinishedTracked += (project, _) => correlatedProjects.Add(project);
        tracker.TargetStartedTracked += (project, _) => correlatedProjects.Add(project);
        tracker.TargetFinishedTracked += (project, _) => correlatedProjects.Add(project);
        tracker.TaskStartedTracked += (project, _) => correlatedProjects.Add(project);
        tracker.TaskFinishedTracked += (project, _) => correlatedProjects.Add(project);
        tracker.MessageTracked += (project, _) => correlatedProjects.Add(project);
        tracker.WarningTracked += (project, _) => correlatedProjects.Add(project);
        tracker.ErrorTracked += (project, _) => correlatedProjects.Add(project);
        tracker.Attach(eventSource);

        BuildEventContext context = CreateContext(evaluationId: 2, projectContextId: 3, nodeId: 4);
        eventSource.InvokeBuildStarted(new BuildStartedEventArgs(string.Empty, string.Empty));
        eventSource.InvokeStatusEventRaised(new ProjectEvaluationFinishedEventArgs
        {
            ProjectFile = "evaluated.proj",
            Properties = new Dictionary<string, string>
            {
                ["TargetFramework"] = "net11.0",
                ["RuntimeIdentifier"] = "win-x64",
            },
            BuildEventContext = context,
        });
        eventSource.InvokeProjectStarted(new ProjectStartedEventArgs(
            string.Empty,
            string.Empty,
            "built.proj",
            "Build",
            new Dictionary<string, string>(),
            new List<DictionaryEntry>())
        {
            BuildEventContext = context,
        });

        eventSource.InvokeTargetStarted(new TargetStartedEventArgs(null, null, "Build", "built.proj", "built.targets") { BuildEventContext = context });
        eventSource.InvokeTargetFinished(new TargetFinishedEventArgs(null, null, "Build", "built.proj", "built.targets", true) { BuildEventContext = context });
        eventSource.InvokeTaskStarted(new TaskStartedEventArgs(null, null, "built.proj", "task.dll", "Task") { BuildEventContext = context });
        eventSource.InvokeTaskFinished(new TaskFinishedEventArgs(null, null, "built.proj", "task.dll", "Task", true) { BuildEventContext = context });
        eventSource.InvokeMessageRaised(new BuildMessageEventArgs("message", null, null, MessageImportance.High) { BuildEventContext = context });
        eventSource.InvokeWarningRaised(new BuildWarningEventArgs(null, "CODE", null, 0, 0, 0, 0, "warning", null, null) { BuildEventContext = context });
        eventSource.InvokeErrorRaised(new BuildErrorEventArgs(null, "CODE", null, 0, 0, 0, 0, "error", null, null) { BuildEventContext = context });
        eventSource.InvokeProjectFinished(new ProjectFinishedEventArgs(null, null, "built.proj", true) { BuildEventContext = context });

        startedProject.ShouldNotBeNull();
        BuildEventTracker.ProjectSnapshot projectStartedSnapshot = startedProject.Value;
        projectStartedSnapshot.ProjectContextId.ShouldBe(3);
        projectStartedSnapshot.NodeId.ShouldBe(4);
        projectStartedSnapshot.EvaluationId.ShouldBe(2);
        projectStartedSnapshot.ProjectFile.ShouldBe("built.proj");
        projectStartedSnapshot.EvaluationProjectFile.ShouldBe("evaluated.proj");
        projectStartedSnapshot.TargetFramework.ShouldBe("net11.0");
        projectStartedSnapshot.RuntimeIdentifier.ShouldBe("win-x64");
        correlatedProjects.Count.ShouldBe(8);
        correlatedProjects.ShouldAllBe(project => project.HasValue && project.Value.ContextKey == projectStartedSnapshot.ContextKey);
    }

    [Fact]
    public void ReportsNullCorrelationForUnknownAndClearedProjectContexts()
    {
        var eventSource = new MockBuildEventSink(0);
        var tracker = new BuildEventTracker();
        var correlatedProjects = new List<BuildEventTracker.ProjectSnapshot?>();

        tracker.WarningTracked += (project, _) => correlatedProjects.Add(project);
        tracker.Attach(eventSource);

        BuildEventContext context = CreateContext(evaluationId: 1, projectContextId: 2, nodeId: 3);
        BuildWarningEventArgs warning = new(null, "CODE", null, 0, 0, 0, 0, "warning", null, null)
        {
            BuildEventContext = context,
        };

        eventSource.InvokeWarningRaised(warning);
        eventSource.InvokeProjectStarted(new ProjectStartedEventArgs(
            string.Empty,
            string.Empty,
            "built.proj",
            "Build",
            new Dictionary<string, string>(),
            new List<DictionaryEntry>())
        {
            BuildEventContext = context,
        });
        eventSource.InvokeBuildStarted(new BuildStartedEventArgs(string.Empty, string.Empty));
        eventSource.InvokeWarningRaised(warning);

        correlatedProjects.ShouldBe([null, null]);
    }

    [Fact]
    public void DistinguishesSameProjectContextIdOnDifferentNodes()
    {
        var eventSource = new MockBuildEventSink(0);
        var tracker = new BuildEventTracker();
        var correlatedProjectFiles = new List<string?>();

        tracker.WarningTracked += (project, _) => correlatedProjectFiles.Add(project?.ProjectFile);
        tracker.Attach(eventSource);

        BuildEventContext firstContext = CreateContext(evaluationId: 1, projectContextId: 7, nodeId: 1);
        BuildEventContext secondContext = CreateContext(evaluationId: 2, projectContextId: 7, nodeId: 2);

        eventSource.InvokeProjectStarted(CreateProjectStartedEvent("first.proj", firstContext));
        eventSource.InvokeProjectStarted(CreateProjectStartedEvent("second.proj", secondContext));
        eventSource.InvokeWarningRaised(CreateWarningEvent(firstContext));
        eventSource.InvokeWarningRaised(CreateWarningEvent(secondContext));

        correlatedProjectFiles.ShouldBe(["first.proj", "second.proj"]);
    }

    [Fact]
    public void TracksBuildDurationAndStopsTrackingAfterDetach()
    {
        var eventSource = new MockBuildEventSink(0);
        var tracker = new BuildEventTracker();
        BuildEventTracker.BuildFinishedSnapshot? finishedBuild = null;
        int warningCount = 0;

        tracker.BuildFinishedTracked += build => finishedBuild = build;
        tracker.WarningTracked += (_, _) => warningCount++;
        tracker.Attach(eventSource);

        DateTime startTime = new(2026, 8, 6, 10, 0, 0);
        eventSource.InvokeBuildStarted(new BuildStartedEventArgs(string.Empty, string.Empty, startTime));
        eventSource.InvokeBuildFinished(new BuildFinishedEventArgs(string.Empty, string.Empty, true, startTime.AddSeconds(5)));

        finishedBuild.ShouldNotBeNull();
        finishedBuild.Value.Duration.ShouldBe(TimeSpan.FromSeconds(5));

        tracker.Detach();
        eventSource.InvokeWarningRaised(new BuildWarningEventArgs(null, "CODE", null, 0, 0, 0, 0, "warning", null, null));

        warningCount.ShouldBe(0);
    }

    [Fact]
    public void MaintainsProjectLifecycleState()
    {
        var eventSource = new MockBuildEventSink(0);
        var stopwatch = new MockStopwatch();
        var tracker = new BuildEventTracker
        {
            StopwatchFactory = () => stopwatch,
        };
        BuildEventTracker.ProjectSnapshot? startedProject = null;
        BuildEventTracker.ProjectSnapshot? targetStartedProject = null;
        BuildEventTracker.ProjectSnapshot? taskStartedProject = null;
        BuildEventTracker.ProjectSnapshot? taskFinishedProject = null;
        BuildEventTracker.ProjectSnapshot? warningProject = null;
        BuildEventTracker.ProjectSnapshot? errorProject = null;
        BuildEventTracker.ProjectSnapshot? finishedProject = null;

        tracker.ProjectStartedTracked += project => startedProject = project;
        tracker.TargetStartedTracked += (project, _) => targetStartedProject = project;
        tracker.TaskStartedTracked += (project, _) => taskStartedProject = project;
        tracker.TaskFinishedTracked += (project, _) => taskFinishedProject = project;
        tracker.WarningTracked += (project, _) => warningProject = project;
        tracker.ErrorTracked += (project, _) => errorProject = project;
        tracker.ProjectFinishedTracked += (project, _) => finishedProject = project;
        tracker.Attach(eventSource);

        BuildEventContext context = CreateContext(evaluationId: 1, projectContextId: 2, nodeId: 3);
        eventSource.InvokeProjectStarted(CreateProjectStartedEvent("built.proj", context));

        startedProject.ShouldNotBeNull();
        stopwatch.IsStarted.ShouldBeTrue();
        startedProject.Value.CurrentTarget.ShouldBeNull();
        startedProject.Value.Succeeded.ShouldBeNull();
        startedProject.Value.WarningCount.ShouldBe(0);
        startedProject.Value.ErrorCount.ShouldBe(0);
        startedProject.Value.IsTimingActive.ShouldBeTrue();

        eventSource.InvokeTargetStarted(new TargetStartedEventArgs(null, null, "Build", "built.proj", "built.targets")
        {
            BuildEventContext = context,
        });

        targetStartedProject.ShouldNotBeNull();
        targetStartedProject.Value.CurrentTarget.ShouldBe("Build");

        eventSource.InvokeTaskStarted(new TaskStartedEventArgs(null, null, "built.proj", "task.dll", "MSBuild")
        {
            BuildEventContext = context,
        });

        stopwatch.IsStarted.ShouldBeFalse();
        taskStartedProject.ShouldNotBeNull();
        taskStartedProject.Value.IsTimingActive.ShouldBeFalse();

        eventSource.InvokeTaskFinished(new TaskFinishedEventArgs(null, null, "built.proj", "task.dll", "MSBuild", true)
        {
            BuildEventContext = context,
        });

        stopwatch.IsStarted.ShouldBeTrue();
        taskFinishedProject.ShouldNotBeNull();
        taskFinishedProject.Value.IsTimingActive.ShouldBeTrue();

        eventSource.InvokeWarningRaised(CreateWarningEvent(context));
        eventSource.InvokeErrorRaised(new BuildErrorEventArgs(null, "CODE", null, 0, 0, 0, 0, "error", null, null)
        {
            BuildEventContext = context,
        });

        warningProject.ShouldNotBeNull();
        warningProject.Value.WarningCount.ShouldBe(1);
        warningProject.Value.ErrorCount.ShouldBe(0);
        errorProject.ShouldNotBeNull();
        errorProject.Value.WarningCount.ShouldBe(1);
        errorProject.Value.ErrorCount.ShouldBe(1);

        eventSource.InvokeProjectFinished(new ProjectFinishedEventArgs(null, null, "built.proj", true)
        {
            BuildEventContext = context,
        });

        finishedProject.ShouldNotBeNull();
        finishedProject.Value.Succeeded.ShouldBe(true);
        finishedProject.Value.IsTimingActive.ShouldBeFalse();
        stopwatch.IsStarted.ShouldBeFalse();

        startedProject.Value.CurrentTarget.ShouldBeNull();
        startedProject.Value.Succeeded.ShouldBeNull();
        startedProject.Value.WarningCount.ShouldBe(0);
        startedProject.Value.ErrorCount.ShouldBe(0);
    }

    [Fact]
    public void OnlyKeepsInitialRestoreTimingActiveDuringMSBuildTasks()
    {
        var eventSource = new MockBuildEventSink(0);
        MockStopwatch[] stopwatches = [new(), new()];
        int stopwatchIndex = 0;
        var tracker = new BuildEventTracker
        {
            StopwatchFactory = () => stopwatches[stopwatchIndex++],
        };

        tracker.Attach(eventSource);
        eventSource.InvokeBuildStarted(new BuildStartedEventArgs(string.Empty, string.Empty));

        BuildEventContext initialRestoreContext = CreateContext(evaluationId: 1, projectContextId: 2, nodeId: 3);
        eventSource.InvokeProjectStarted(CreateProjectStartedEvent("initial.proj", initialRestoreContext, "Restore"));
        eventSource.InvokeTaskStarted(new TaskStartedEventArgs(null, null, "initial.proj", "task.dll", "MSBuild")
        {
            BuildEventContext = initialRestoreContext,
        });
        stopwatches[0].IsStarted.ShouldBeTrue();
        eventSource.InvokeTaskFinished(new TaskFinishedEventArgs(null, null, "initial.proj", "task.dll", "MSBuild", true)
        {
            BuildEventContext = initialRestoreContext,
        });
        stopwatches[0].IsStarted.ShouldBeTrue();
        eventSource.InvokeProjectFinished(new ProjectFinishedEventArgs(null, null, "initial.proj", true)
        {
            BuildEventContext = initialRestoreContext,
        });

        BuildEventContext laterRestoreContext = CreateContext(evaluationId: 4, projectContextId: 5, nodeId: 3);
        eventSource.InvokeProjectStarted(CreateProjectStartedEvent("later.proj", laterRestoreContext, "Restore"));
        eventSource.InvokeTaskStarted(new TaskStartedEventArgs(null, null, "later.proj", "task.dll", "MSBuild")
        {
            BuildEventContext = laterRestoreContext,
        });
        stopwatches[1].IsStarted.ShouldBeFalse();
        eventSource.InvokeTaskFinished(new TaskFinishedEventArgs(null, null, "later.proj", "task.dll", "MSBuild", true)
        {
            BuildEventContext = laterRestoreContext,
        });
        stopwatches[1].IsStarted.ShouldBeTrue();
    }

    private static BuildEventContext CreateContext(int evaluationId, int projectContextId, int nodeId)
        => new(
            submissionId: -1,
            nodeId,
            evaluationId,
            projectInstanceId: -1,
            projectContextId,
            targetId: 1,
            taskId: 1);

    private static ProjectStartedEventArgs CreateProjectStartedEvent(
        string projectFile,
        BuildEventContext context,
        string targetNames = "Build")
        => new(
            string.Empty,
            string.Empty,
            projectFile,
            targetNames,
            new Dictionary<string, string>(),
            new List<DictionaryEntry>())
        {
            BuildEventContext = context,
        };

    private static BuildWarningEventArgs CreateWarningEvent(BuildEventContext context)
        => new(null, "CODE", null, 0, 0, 0, 0, "warning", null, null)
        {
            BuildEventContext = context,
        };
}
