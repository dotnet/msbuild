// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging;

/// <summary>
/// Correlates build events and stores presentation-neutral project state.
/// </summary>
/// <remarks>
/// The tracker owns mutable project lifecycle and diagnostic state.
/// Lifecycle callbacks receive immutable snapshots.
/// Other callbacks receive context keys so consumers can filter events before they request snapshots.
/// Tracked state remains available during <see cref="BuildFinishedTracked"/> callbacks and is cleared afterward.
/// </remarks>
internal sealed class BuildEventTracker
{
    private readonly record struct EvalProjectInfo(
        string? ProjectFile,
        string? TargetFramework,
        string? RuntimeIdentifier);

    private sealed class TrackedProjectState
    {
        internal TrackedProjectState(
            ProjectContextKey contextKey,
            int evaluationId,
            string? projectFile,
            string? targetNames,
            string? evaluationProjectFile,
            string? targetFramework,
            string? runtimeIdentifier)
        {
            ContextKey = contextKey;
            EvaluationId = evaluationId;
            ProjectFile = projectFile;
            TargetNames = targetNames;
            EvaluationProjectFile = evaluationProjectFile;
            TargetFramework = targetFramework;
            RuntimeIdentifier = runtimeIdentifier;
        }

        internal ProjectContextKey ContextKey { get; }

        internal int ProjectContextId => ContextKey.ProjectContextId;

        internal int NodeId => ContextKey.NodeId;

        internal int EvaluationId { get; }

        internal string? ProjectFile { get; }

        internal string? TargetNames { get; }

        internal string? EvaluationProjectFile { get; }

        internal string? TargetFramework { get; }

        internal string? RuntimeIdentifier { get; }

        internal string? CurrentTarget { get; private set; }

        internal bool? Succeeded { get; private set; }

        internal int ErrorCount { get; private set; }

        internal int WarningCount { get; private set; }

        internal void StartTarget(string targetName)
        {
            CurrentTarget = targetName;
        }

        internal void AddWarning()
        {
            WarningCount++;
        }

        internal void AddError()
        {
            ErrorCount++;
        }

        internal void Finish(bool succeeded)
        {
            Succeeded = succeeded;
        }

        internal ProjectSnapshot CreateSnapshot() => new(
            ContextKey,
            EvaluationId,
            ProjectFile,
            TargetNames,
            EvaluationProjectFile,
            TargetFramework,
            RuntimeIdentifier,
            CurrentTarget,
            Succeeded,
            ErrorCount,
            WarningCount);
    }

    internal readonly record struct BuildStartedSnapshot(DateTime Timestamp);

    internal readonly record struct BuildFinishedSnapshot(DateTime Timestamp, TimeSpan Duration, bool Succeeded);

    /// <summary>
    /// Immutable project lifecycle state captured when a tracked event is raised.
    /// </summary>
    internal readonly record struct ProjectSnapshot(
        ProjectContextKey ContextKey,
        int EvaluationId,
        string? ProjectFile,
        string? TargetNames,
        string? EvaluationProjectFile,
        string? TargetFramework,
        string? RuntimeIdentifier,
        string? CurrentTarget,
        bool? Succeeded,
        int ErrorCount,
        int WarningCount)
    {
        internal int ProjectContextId => ContextKey.ProjectContextId;

        internal int NodeId => ContextKey.NodeId;
    }

    private IEventSource? _eventSource;

    internal event Action<BuildStartedSnapshot>? BuildStartedTracked;

    internal event Action<BuildFinishedSnapshot>? BuildFinishedTracked;

    internal event Action<ProjectSnapshot>? ProjectStartedTracked;

    internal event Action<ProjectSnapshot?, ProjectFinishedEventArgs>? ProjectFinishedTracked;

    internal event Action<ProjectContextKey?, TargetStartedEventArgs>? TargetStartedTracked;

    internal event Action<ProjectContextKey?, TargetFinishedEventArgs>? TargetFinishedTracked;

    internal event Action<ProjectContextKey?, TaskStartedEventArgs>? TaskStartedTracked;

    internal event Action<ProjectContextKey?, TaskFinishedEventArgs>? TaskFinishedTracked;

    internal event Action<BuildStatusEventArgs>? StatusEventTracked;

    internal event Action<ProjectContextKey?, BuildMessageEventArgs>? MessageTracked;

    internal event Action<ProjectContextKey?, BuildWarningEventArgs>? WarningTracked;

    internal event Action<ProjectContextKey?, BuildErrorEventArgs>? ErrorTracked;

    internal DateTime BuildStartTime { get; private set; }

    /// <summary>
    /// Composite identity of a project request in a logger event stream.
    /// The identity consists of the build node ID and that node's project context ID.
    /// </summary>
    internal readonly record struct ProjectContextKey(int NodeId, int ProjectContextId)
    {
        public ProjectContextKey(BuildEventContext context)
            : this(context.NodeId, context.ProjectContextId)
        {
        }
    }

    /// <summary>
    /// A wrapper over the evaluation context ID passed to us in <see cref="IEventSource"/> logger events.
    /// </summary>
    internal readonly record struct EvalContext(int Id)
    {
        public EvalContext(BuildEventContext context)
            : this(context.EvaluationId)
        {
        }
    }

    /// <summary>
    /// Tracks the status of all relevant projects seen so far.
    /// </summary>
    /// <remarks>
    /// Keyed by the build node ID and node-local project context ID passed to logger callbacks.
    /// </remarks>
    private readonly Dictionary<ProjectContextKey, TrackedProjectState> _projects = [];

    private readonly Dictionary<EvalContext, EvalProjectInfo> _projectEvaluations = [];

    internal void Attach(IEventSource eventSource)
    {
        ArgumentNullException.ThrowIfNull(eventSource);

        Detach();
        _eventSource = eventSource;

        eventSource.BuildStarted += OnBuildStarted;
        eventSource.BuildFinished += OnBuildFinished;
        eventSource.ProjectStarted += OnProjectStarted;
        eventSource.ProjectFinished += OnProjectFinished;
        eventSource.TargetStarted += OnTargetStarted;
        eventSource.TargetFinished += OnTargetFinished;
        eventSource.TaskStarted += OnTaskStarted;
        eventSource.TaskFinished += OnTaskFinished;
        eventSource.StatusEventRaised += OnStatusEventRaised;
        eventSource.MessageRaised += OnMessageRaised;
        eventSource.WarningRaised += OnWarningRaised;
        eventSource.ErrorRaised += OnErrorRaised;
    }

    internal void Detach()
    {
        if (_eventSource is not null)
        {
            _eventSource.BuildStarted -= OnBuildStarted;
            _eventSource.BuildFinished -= OnBuildFinished;
            _eventSource.ProjectStarted -= OnProjectStarted;
            _eventSource.ProjectFinished -= OnProjectFinished;
            _eventSource.TargetStarted -= OnTargetStarted;
            _eventSource.TargetFinished -= OnTargetFinished;
            _eventSource.TaskStarted -= OnTaskStarted;
            _eventSource.TaskFinished -= OnTaskFinished;
            _eventSource.StatusEventRaised -= OnStatusEventRaised;
            _eventSource.MessageRaised -= OnMessageRaised;
            _eventSource.WarningRaised -= OnWarningRaised;
            _eventSource.ErrorRaised -= OnErrorRaised;
            _eventSource = null;
        }

        ClearState();
    }

    /// <summary>
    /// Creates an immutable snapshot of the current state for a tracked project.
    /// </summary>
    internal bool TryGetProjectSnapshot(ProjectContextKey contextKey, out ProjectSnapshot snapshot)
    {
        if (_projects.TryGetValue(contextKey, out TrackedProjectState? project))
        {
            snapshot = project.CreateSnapshot();
            return true;
        }

        snapshot = default;
        return false;
    }

    private void OnBuildStarted(object sender, BuildStartedEventArgs e)
    {
        ClearState();

        BuildStartTime = e.Timestamp;
        BuildStartedTracked?.Invoke(new BuildStartedSnapshot(e.Timestamp));
    }

    private void OnBuildFinished(object sender, BuildFinishedEventArgs e)
    {
        try
        {
            BuildFinishedTracked?.Invoke(new BuildFinishedSnapshot(
                e.Timestamp,
                e.Timestamp - BuildStartTime,
                e.Succeeded));
        }
        finally
        {
            ClearState();
        }
    }

    private void OnProjectStarted(object sender, ProjectStartedEventArgs e)
    {
        BuildEventContext? buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        _projectEvaluations.TryGetValue(new EvalContext(buildEventContext), out EvalProjectInfo evalInfo);

        TrackedProjectState project = new(
            new ProjectContextKey(buildEventContext),
            buildEventContext.EvaluationId,
            e.ProjectFile,
            e.TargetNames,
            evalInfo.ProjectFile,
            evalInfo.TargetFramework,
            evalInfo.RuntimeIdentifier);

        _projects[project.ContextKey] = project;

        ProjectStartedTracked?.Invoke(project.CreateSnapshot());
    }

    private void OnProjectFinished(object sender, ProjectFinishedEventArgs e)
    {
        TrackedProjectState? project = CorrelateProject(e);
        if (project is not null)
        {
            project.Finish(e.Succeeded);
        }

        ProjectFinishedTracked?.Invoke(project?.CreateSnapshot(), e);
    }

    private void OnTargetStarted(object sender, TargetStartedEventArgs e)
    {
        TrackedProjectState? project = CorrelateProject(e);
        project?.StartTarget(e.TargetName);
        TargetStartedTracked?.Invoke(project?.ContextKey, e);
    }

    private void OnTargetFinished(object sender, TargetFinishedEventArgs e)
    {
        TargetFinishedTracked?.Invoke(CorrelateProject(e)?.ContextKey, e);
    }

    private void OnTaskStarted(object sender, TaskStartedEventArgs e)
    {
        TaskStartedTracked?.Invoke(CorrelateProject(e)?.ContextKey, e);
    }

    private void OnTaskFinished(object sender, TaskFinishedEventArgs e)
    {
        TaskFinishedTracked?.Invoke(CorrelateProject(e)?.ContextKey, e);
    }

    private void OnStatusEventRaised(object sender, BuildStatusEventArgs e)
    {
        if (e is ProjectEvaluationFinishedEventArgs evalFinish)
        {
            CaptureEvalContext(evalFinish);
        }

        StatusEventTracked?.Invoke(e);
    }

    private void OnMessageRaised(object sender, BuildMessageEventArgs e)
    {
        // Consumers can filter high-volume messages before they request a project snapshot.
        MessageTracked?.Invoke(GetProjectContextKey(e), e);
    }

    private void OnWarningRaised(object sender, BuildWarningEventArgs e)
    {
        TrackedProjectState? project = CorrelateProject(e);
        project?.AddWarning();
        WarningTracked?.Invoke(project?.ContextKey, e);
    }

    private void OnErrorRaised(object sender, BuildErrorEventArgs e)
    {
        TrackedProjectState? project = CorrelateProject(e);
        project?.AddError();
        ErrorTracked?.Invoke(project?.ContextKey, e);
    }

    private void CaptureEvalContext(ProjectEvaluationFinishedEventArgs evalFinish)
    {
        var buildEventContext = evalFinish.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        EvalContext c = new(buildEventContext);

        if (!_projectEvaluations.TryGetValue(c, out EvalProjectInfo _))
        {
            string? tfm = null;
            string? rid = null;
            foreach (var property in evalFinish.EnumerateProperties())
            {
                if (tfm is not null && rid is not null)
                {
                    // We already have both properties, no need to continue.
                    break;
                }
                switch (property.Name)
                {
                    case "TargetFramework":
                        tfm = property.Value;
                        break;
                    case "RuntimeIdentifier":
                        rid = property.Value;
                        break;
                }
            }
            var evalInfo = new EvalProjectInfo(evalFinish.ProjectFile, tfm, rid);
            _projectEvaluations[c] = evalInfo;
        }
    }

    private TrackedProjectState? CorrelateProject(BuildEventArgs e)
    {
        ProjectContextKey? contextKey = GetProjectContextKey(e);
        return contextKey is not null
            && _projects.TryGetValue(
                contextKey.Value,
                out TrackedProjectState? project)
                    ? project
                    : null;
    }

    private static ProjectContextKey? GetProjectContextKey(BuildEventArgs e) =>
        e.BuildEventContext is BuildEventContext context
            ? new ProjectContextKey(context)
            : null;

    private void ClearState()
    {
        _projects.Clear();
        _projectEvaluations.Clear();
    }
}