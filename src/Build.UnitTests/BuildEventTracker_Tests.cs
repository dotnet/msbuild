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
    public void EventCallbacks_ProjectEvents_CorrelateWithEvaluationData()
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
    public void EventCallbacks_UnknownAndClearedProjectContexts_ReportNullCorrelation()
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
    public void EventCallbacks_SameProjectContextIdOnDifferentNodes_DistinguishProjects()
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
    public void Detach_AllEvents_StopTracking()
    {
        var eventSource = new MockBuildEventSink(0);
        var tracker = new BuildEventTracker();
        BuildEventTracker.BuildFinishedSnapshot? finishedBuild = null;
        int trackedEventCount = 0;

        tracker.BuildStartedTracked += _ => trackedEventCount++;
        tracker.BuildFinishedTracked += build =>
        {
            finishedBuild = build;
            trackedEventCount++;
        };
        tracker.ProjectStartedTracked += _ => trackedEventCount++;
        tracker.ProjectFinishedTracked += (_, _) => trackedEventCount++;
        tracker.TargetStartedTracked += (_, _) => trackedEventCount++;
        tracker.TargetFinishedTracked += (_, _) => trackedEventCount++;
        tracker.TaskStartedTracked += (_, _) => trackedEventCount++;
        tracker.TaskFinishedTracked += (_, _) => trackedEventCount++;
        tracker.StatusEventTracked += _ => trackedEventCount++;
        tracker.MessageTracked += (_, _) => trackedEventCount++;
        tracker.WarningTracked += (_, _) => trackedEventCount++;
        tracker.ErrorTracked += (_, _) => trackedEventCount++;
        tracker.Attach(eventSource);

        DateTime startTime = new(2026, 8, 6, 10, 0, 0);
        BuildEventContext context = CreateContext(evaluationId: 1, projectContextId: 2, nodeId: 3);
        RaiseAllTrackedEvents(eventSource, context, startTime);

        finishedBuild.ShouldNotBeNull();
        finishedBuild.Value.Duration.ShouldBe(TimeSpan.FromSeconds(5));
        trackedEventCount.ShouldBe(12);

        tracker.Detach();
        RaiseAllTrackedEvents(eventSource, context, startTime);

        trackedEventCount.ShouldBe(12);
    }

    [Fact]
    public void EventCallbacks_ProjectLifecycle_ProduceImmutableSnapshots()
    {
        var eventSource = new MockBuildEventSink(0);
        var tracker = new BuildEventTracker();
        BuildEventTracker.ProjectSnapshot? startedProject = null;
        BuildEventTracker.ProjectSnapshot? targetStartedProject = null;
        BuildEventTracker.ProjectSnapshot? warningProject = null;
        BuildEventTracker.ProjectSnapshot? errorProject = null;
        BuildEventTracker.ProjectSnapshot? finishedProject = null;

        tracker.ProjectStartedTracked += project => startedProject = project;
        tracker.TargetStartedTracked += (project, _) => targetStartedProject = project;
        tracker.WarningTracked += (project, _) => warningProject = project;
        tracker.ErrorTracked += (project, _) => errorProject = project;
        tracker.ProjectFinishedTracked += (project, _) => finishedProject = project;
        tracker.Attach(eventSource);

        BuildEventContext context = CreateContext(evaluationId: 1, projectContextId: 2, nodeId: 3);
        eventSource.InvokeProjectStarted(CreateProjectStartedEvent("built.proj", context));

        startedProject.ShouldNotBeNull();
        startedProject.Value.CurrentTarget.ShouldBeNull();
        startedProject.Value.Succeeded.ShouldBeNull();
        startedProject.Value.WarningCount.ShouldBe(0);
        startedProject.Value.ErrorCount.ShouldBe(0);

        eventSource.InvokeTargetStarted(new TargetStartedEventArgs(null, null, "Build", "built.proj", "built.targets")
        {
            BuildEventContext = context,
        });

        targetStartedProject.ShouldNotBeNull();
        targetStartedProject.Value.CurrentTarget.ShouldBe("Build");

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

        startedProject.Value.CurrentTarget.ShouldBeNull();
        startedProject.Value.Succeeded.ShouldBeNull();
        startedProject.Value.WarningCount.ShouldBe(0);
        startedProject.Value.ErrorCount.ShouldBe(0);
    }

    [Fact]
    public void Attach_CalledAgain_DetachesPreviousEventSource()
    {
        var firstEventSource = new MockBuildEventSink(0);
        var secondEventSource = new MockBuildEventSink(1);
        var tracker = new BuildEventTracker();
        int warningCount = 0;

        tracker.WarningTracked += (_, _) => warningCount++;
        tracker.Attach(firstEventSource);
        tracker.Attach(firstEventSource);
        firstEventSource.InvokeWarningRaised(CreateWarningEvent(CreateContext(1, 2, 3)));

        tracker.Attach(secondEventSource);
        firstEventSource.InvokeWarningRaised(CreateWarningEvent(CreateContext(1, 2, 3)));
        secondEventSource.InvokeWarningRaised(CreateWarningEvent(CreateContext(1, 2, 3)));

        warningCount.ShouldBe(2);
    }

    [Fact]
    public void BuildStarted_CachedEvaluation_ClearsEvaluationData()
    {
        var eventSource = new MockBuildEventSink(0);
        var tracker = new BuildEventTracker();
        var startedProjects = new List<BuildEventTracker.ProjectSnapshot>();
        BuildEventContext firstContext = CreateContext(evaluationId: 1, projectContextId: 2, nodeId: 3);
        BuildEventContext secondContext = CreateContext(evaluationId: 1, projectContextId: 4, nodeId: 3);

        tracker.ProjectStartedTracked += startedProjects.Add;
        tracker.Attach(eventSource);

        eventSource.InvokeBuildStarted(new BuildStartedEventArgs(string.Empty, string.Empty));
        eventSource.InvokeStatusEventRaised(CreateEvaluationFinishedEvent("evaluated.proj", firstContext));
        eventSource.InvokeProjectStarted(CreateProjectStartedEvent("built.proj", firstContext));
        eventSource.InvokeProjectFinished(new ProjectFinishedEventArgs(null, null, "built.proj", true)
        {
            BuildEventContext = firstContext,
        });
        eventSource.InvokeBuildFinished(new BuildFinishedEventArgs(string.Empty, string.Empty, true));

        eventSource.InvokeBuildStarted(new BuildStartedEventArgs(string.Empty, string.Empty));
        eventSource.InvokeProjectStarted(CreateProjectStartedEvent("built.proj", secondContext));

        startedProjects.Count.ShouldBe(2);
        startedProjects[1].EvaluationProjectFile.ShouldBeNull();
        startedProjects[1].TargetFramework.ShouldBeNull();
        startedProjects[1].RuntimeIdentifier.ShouldBeNull();
    }

    [Fact]
    public void EvaluationFinished_RepeatedEvaluationId_KeepsFirstEvaluation()
    {
        var eventSource = new MockBuildEventSink(0);
        var tracker = new BuildEventTracker();
        BuildEventTracker.ProjectSnapshot? startedProject = null;
        BuildEventContext context = CreateContext(evaluationId: 1, projectContextId: 2, nodeId: 3);

        tracker.ProjectStartedTracked += project => startedProject = project;
        tracker.Attach(eventSource);

        eventSource.InvokeStatusEventRaised(CreateEvaluationFinishedEvent("first.proj", context));
        eventSource.InvokeStatusEventRaised(new ProjectEvaluationFinishedEventArgs
        {
            ProjectFile = "second.proj",
            Properties = new Dictionary<string, string>
            {
                ["TargetFramework"] = "net12.0",
                ["RuntimeIdentifier"] = "linux-x64",
            },
            BuildEventContext = context,
        });
        eventSource.InvokeProjectStarted(CreateProjectStartedEvent("built.proj", context));

        startedProject.ShouldNotBeNull();
        startedProject.Value.EvaluationProjectFile.ShouldBe("first.proj");
        startedProject.Value.TargetFramework.ShouldBe("net11.0");
        startedProject.Value.RuntimeIdentifier.ShouldBe("win-x64");
    }

    [Fact]
    public void EventCallbacks_NullBuildEventContext_DoNotCorrelateProject()
    {
        var eventSource = new MockBuildEventSink(0);
        var tracker = new BuildEventTracker();
        int projectStartedCount = 0;
        int statusEventCount = 0;
        BuildEventTracker.ProjectSnapshot? warningProject = null;

        tracker.ProjectStartedTracked += _ => projectStartedCount++;
        tracker.StatusEventTracked += _ => statusEventCount++;
        tracker.WarningTracked += (project, _) => warningProject = project;
        tracker.Attach(eventSource);

        eventSource.InvokeStatusEventRaised(new ProjectEvaluationFinishedEventArgs
        {
            ProjectFile = "evaluated.proj",
            Properties = new Dictionary<string, string>(),
        });
        eventSource.InvokeProjectStarted(new ProjectStartedEventArgs(
            string.Empty,
            string.Empty,
            "built.proj",
            "Build",
            new Dictionary<string, string>(),
            new List<DictionaryEntry>()));
        eventSource.InvokeWarningRaised(new BuildWarningEventArgs(
            null,
            "CODE",
            null,
            0,
            0,
            0,
            0,
            "warning",
            null,
            null));

        projectStartedCount.ShouldBe(0);
        statusEventCount.ShouldBe(1);
        warningProject.ShouldBeNull();
    }

    private static void RaiseAllTrackedEvents(
        MockBuildEventSink eventSource,
        BuildEventContext context,
        DateTime startTime)
    {
        eventSource.InvokeBuildStarted(new BuildStartedEventArgs(string.Empty, string.Empty, startTime));
        eventSource.InvokeStatusEventRaised(new ProjectEvaluationStartedEventArgs
        {
            BuildEventContext = context,
        });
        eventSource.InvokeProjectStarted(CreateProjectStartedEvent("built.proj", context));
        eventSource.InvokeTargetStarted(new TargetStartedEventArgs(null, null, "Build", "built.proj", "built.targets")
        {
            BuildEventContext = context,
        });
        eventSource.InvokeTargetFinished(new TargetFinishedEventArgs(null, null, "Build", "built.proj", "built.targets", true)
        {
            BuildEventContext = context,
        });
        eventSource.InvokeTaskStarted(new TaskStartedEventArgs(null, null, "built.proj", "task.dll", "Task")
        {
            BuildEventContext = context,
        });
        eventSource.InvokeTaskFinished(new TaskFinishedEventArgs(null, null, "built.proj", "task.dll", "Task", true)
        {
            BuildEventContext = context,
        });
        eventSource.InvokeMessageRaised(new BuildMessageEventArgs("message", null, null, MessageImportance.High)
        {
            BuildEventContext = context,
        });
        eventSource.InvokeWarningRaised(CreateWarningEvent(context));
        eventSource.InvokeErrorRaised(new BuildErrorEventArgs(null, "CODE", null, 0, 0, 0, 0, "error", null, null)
        {
            BuildEventContext = context,
        });
        eventSource.InvokeProjectFinished(new ProjectFinishedEventArgs(null, null, "built.proj", true)
        {
            BuildEventContext = context,
        });
        eventSource.InvokeBuildFinished(new BuildFinishedEventArgs(
            string.Empty,
            string.Empty,
            true,
            startTime.AddSeconds(5)));
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

    private static ProjectEvaluationFinishedEventArgs CreateEvaluationFinishedEvent(
        string projectFile,
        BuildEventContext context)
        => new()
        {
            ProjectFile = projectFile,
            Properties = new Dictionary<string, string>
            {
                ["TargetFramework"] = "net11.0",
                ["RuntimeIdentifier"] = "win-x64",
            },
            BuildEventContext = context,
        };
}
