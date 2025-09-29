// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging;

/// <summary>
/// A wrapper over the project context ID passed to us in <see cref="IEventSource"/> logger events.
/// </summary>
internal record struct ProjectContext(int Id)
{
    public ProjectContext(BuildEventContext context)
        : this(context.ProjectContextId)
    {
    }
}

/// <summary>
/// A wrapper over the evaluation context ID passed to us in <see cref="IEventSource"/> logger events.
/// </summary>
internal record struct EvalContext(int Id)
{
    public EvalContext(BuildEventContext context)
        : this(context.EvaluationId)
    {
    }
}

/// <summary>
/// Base class that tracks build state (evaluation data, node status, completed project data, and whole-build data) during builds.
/// Subclasses can specialize the type of data tracked for each area and implement rendering logic.
/// </summary>
/// <typeparam name="TEvalData">Data gathered/projected for each evaluation context</typeparam>
/// <typeparam name="TNodeData">Data stored for each live, actively running build worker node</typeparam>
/// <typeparam name="TProjectData">Data stored for each completed project context instance</typeparam>
/// <typeparam name="TBuildData">Data that is aggregated across the entire build session</typeparam>
public abstract class ProjectTrackingLoggerBase<TEvalData, TNodeData, TProjectData, TBuildData> : INodeLogger
{

    /// <summary>
    /// Tracks the evaluation data for all evaluations seen so far.
    /// </summary>
    /// <remarks>
    /// Keyed by an ID that gets passed to logger callbacks, this allows us to quickly look up the corresponding evaluation.
    /// </remarks>
    private readonly Dictionary<EvalContext, TEvalData> _evaluationDataByEvalId = new();

    /// <summary>
    /// Tracks the status of all relevant projects seen so far.
    /// </summary>
    /// <remarks>
    /// Keyed by an ID that gets passed to logger callbacks, this allows us to quickly look up the corresponding project.
    /// </remarks>
    private readonly Dictionary<ProjectContext, TProjectData> _projectDataByProjectContextId = new();

    /// <summary>
    /// Tracks build-level data for the entire build session.
    /// </summary>
    private TBuildData? _buildData;

    #region INodeLogger implementation

    /// <inheritdoc/>
    public abstract LoggerVerbosity Verbosity { get; set; }

    /// <inheritdoc/>
    public abstract string? Parameters { get; set; }

    /// <summary>
    /// The number of nodes in the build. Handles the case where MSBUILDNOINPROCNODE is set by reserving an extra slot.
    /// </summary>
    protected int NodeCount { get; private set; }

    /// <inheritdoc/>
    public virtual void Initialize(IEventSource eventSource, int nodeCount)
    {
        // When MSBUILDNOINPROCNODE enabled, NodeId's reported by build start with 2. We need to reserve an extra spot for this case.
        NodeCount = nodeCount + 1;

        Initialize(eventSource);
    }

    /// <inheritdoc/>
    public virtual void Initialize(IEventSource eventSource)
    {
        eventSource.BuildStarted += BuildStartedHandler;
        eventSource.BuildFinished += BuildFinishedHandler;
        eventSource.ProjectStarted += ProjectStartedHandler;
        eventSource.ProjectFinished += ProjectFinishedHandler;
        eventSource.TargetStarted += TargetStartedHandler;
        eventSource.TargetFinished += TargetFinishedHandler;
        eventSource.TaskStarted += TaskStartedHandler;
        eventSource.StatusEventRaised += StatusEventRaisedHandler;
        eventSource.MessageRaised += MessageRaisedHandler;
        eventSource.WarningRaised += WarningRaisedHandler;
        eventSource.ErrorRaised += ErrorRaisedHandler;

        if (eventSource is IEventSource3 eventSource3)
        {
            eventSource3.IncludeTaskInputs();
        }

        if (eventSource is IEventSource4 eventSource4)
        {
            eventSource4.IncludeEvaluationPropertiesAndItems();
        }
    }

    /// <inheritdoc/>
    public abstract void Shutdown();

    #endregion

    #region Logger callbacks

    /// <summary>
    /// The <see cref="IEventSource.BuildStarted"/> callback.
    /// </summary>
    private void BuildStartedHandler(object sender, BuildStartedEventArgs e)
    {
        _buildData = CreateBuildData(e);
        OnBuildStarted(e, _buildData);
    }

    /// <summary>
    /// The <see cref="IEventSource.BuildFinished"/> callback.
    /// </summary>
    private void BuildFinishedHandler(object sender, BuildFinishedEventArgs e)
    {
        OnBuildFinished(e, _projectDataByProjectContextId.Values.ToArray(), _buildData!);

        // Clear tracking data
        _projectDataByProjectContextId.Clear();
        _evaluationDataByEvalId.Clear();
        _buildData = default;
    }

    /// <summary>
    /// The <see cref="IEventSource.StatusEventRaised"/> callback.
    /// </summary>
    private void StatusEventRaisedHandler(object sender, BuildStatusEventArgs e)
    {
        switch (e)
        {
            case BuildCanceledEventArgs cancelEvent:
                OnBuildCanceled(cancelEvent);
                break;
            case ProjectEvaluationStartedEventArgs:
                break;
            case ProjectEvaluationFinishedEventArgs evalFinish:
                CaptureEvalContext(evalFinish);
                break;
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.ProjectStarted"/> callback.
    /// </summary>
    private void ProjectStartedHandler(object sender, ProjectStartedEventArgs e)
    {
        if (e.BuildEventContext is null)
        {
            return;
        }

        ProjectContext projectContext = new(e.BuildEventContext);
        EvalContext evalContext = new(e.BuildEventContext);

        // Get eval data for this project
        if (_evaluationDataByEvalId.TryGetValue(evalContext, out TEvalData? evalData))
        {
            // Create project data using the eval data
            TProjectData? projectData = CreateProjectData(evalData, e);
            if (projectData != null)
            {
                _projectDataByProjectContextId[projectContext] = projectData;
                OnProjectStarted(e, evalData, projectData!, _buildData!);
            }
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.ProjectFinished"/> callback.
    /// </summary>
    private void ProjectFinishedHandler(object sender, ProjectFinishedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        ProjectContext projectContext = new(buildEventContext);

        // Get project data
        if (_projectDataByProjectContextId.TryGetValue(projectContext, out var projectData))
        {
            OnProjectFinished(e, projectData, _buildData!);
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.TargetStarted"/> callback.
    /// </summary>
    private void TargetStartedHandler(object sender, TargetStartedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null)
        {
            ProjectContext projectContext = new(buildEventContext);
            if (_projectDataByProjectContextId.TryGetValue(projectContext, out TProjectData? projectData))
            {
                OnTargetStarted(e, projectData, _buildData!);
            }
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.TargetFinished"/> callback.
    /// </summary>
    private void TargetFinishedHandler(object sender, TargetFinishedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        ProjectContext projectContext = new(buildEventContext);
        if (_projectDataByProjectContextId.TryGetValue(projectContext, out var projectData))
        {
            OnTargetFinished(e, projectData, _buildData!);
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.TaskStarted"/> callback.
    /// </summary>
    private void TaskStartedHandler(object sender, TaskStartedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null)
        {
            ProjectContext projectContext = new(buildEventContext);
            if (_projectDataByProjectContextId.TryGetValue(projectContext, out var projectData))
            {
                OnTaskStarted(e, projectData, _buildData!);
            }
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.MessageRaised"/> callback.
    /// </summary>
    private void MessageRaisedHandler(object sender, BuildMessageEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        ProjectContext projectContext = new(buildEventContext);
        TProjectData? projectData = default;
        _projectDataByProjectContextId.TryGetValue(projectContext, out projectData);
        OnMessageRaised(e, projectData, _buildData!);
    }

    /// <summary>
    /// The <see cref="IEventSource.WarningRaised"/> callback.
    /// </summary>
    private void WarningRaisedHandler(object sender, BuildWarningEventArgs e)
    {
        BuildEventContext? buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            OnWarningRaised(e, default, _buildData!);
            return;
        }

        ProjectContext projectContext = new(buildEventContext);
        TProjectData? projectData = default;
        _projectDataByProjectContextId.TryGetValue(projectContext, out projectData);
        OnWarningRaised(e, projectData, _buildData!);
    }

    /// <summary>
    /// The <see cref="IEventSource.ErrorRaised"/> callback.
    /// </summary>
    private void ErrorRaisedHandler(object sender, BuildErrorEventArgs e)
    {
        BuildEventContext? buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            OnErrorRaised(e, default, _buildData!);
            return;
        }

        ProjectContext projectContext = new(buildEventContext);
        TProjectData? projectData = default;
        _projectDataByProjectContextId.TryGetValue(projectContext, out projectData);
        OnErrorRaised(e, projectData, _buildData!);
    }

    #endregion

    #region Protected helpers

    protected int? GetNodeIdForEvent(BuildEventArgs args) => args?.BuildEventContext is null ? null : NodeIndexForContext(args.BuildEventContext);

    #endregion

    #region Private helpers

    private int NodeIndexForContext(BuildEventContext context)
    {
        // Node IDs reported by the build are 1-based.
        return context.NodeId - 1;
    }

    /// <summary>
    /// Captures evaluation context data from the evaluation finished event.
    /// </summary>
    private void CaptureEvalContext(ProjectEvaluationFinishedEventArgs evalFinish)
    {
        var buildEventContext = evalFinish.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        EvalContext evalContext = new(buildEventContext);

        if (!_evaluationDataByEvalId.ContainsKey(evalContext))
        {
            TEvalData evalData = CreateEvalData(evalFinish);
            _evaluationDataByEvalId[evalContext] = evalData;
        }
    }

    #endregion

    #region Abstract methods - must be implemented by subclasses

    /// <summary>
    /// Creates evaluation data from the evaluation finished event.
    /// </summary>
    /// <param name="e">The evaluation finished event args.</param>
    /// <returns>The evaluation data to store, or null to not track this evaluation.</returns>
    protected abstract TEvalData CreateEvalData(ProjectEvaluationFinishedEventArgs e);

    /// <summary>
    /// Creates project data from the project started event and evaluation data.
    /// </summary>
    /// <param name="evalData">The evaluation data for this project.</param>
    /// <param name="e">The project started event args.</param>
    /// <returns>The project data to store, or null to not track this project.</returns>
    protected abstract TProjectData? CreateProjectData(TEvalData evalData, ProjectStartedEventArgs e);

    /// <summary>
    /// Creates build data when the build starts.
    /// </summary>
    /// <param name="e">The build started event args.</param>
    /// <returns>The build data to track for this build session.</returns>
    protected abstract TBuildData CreateBuildData(BuildStartedEventArgs e);

    #endregion

    #region Virtual methods - can be overridden by subclasses

    /// <summary>
    /// Called when the build starts.
    /// </summary>
    protected virtual void OnBuildStarted(BuildStartedEventArgs e, TBuildData buildData) { }

    /// <summary>
    /// Called when the build finishes.
    /// </summary>
    protected virtual void OnBuildFinished(BuildFinishedEventArgs e, TProjectData[] projectData, TBuildData buildData) { }

    /// <summary>
    /// Called when the build is canceled.
    /// </summary>
    protected virtual void OnBuildCanceled(BuildCanceledEventArgs e) { }

    /// <summary>
    /// Called when a project starts.
    /// </summary>
    protected virtual void OnProjectStarted(ProjectStartedEventArgs e, TEvalData evalData, TProjectData projectData, TBuildData buildData) { }

    /// <summary>
    /// Called when a project finishes.
    /// </summary>
    protected virtual void OnProjectFinished(ProjectFinishedEventArgs e, TProjectData projectData, TBuildData buildData) { }

    /// <summary>
    /// Called when a target starts.
    /// </summary>
    protected virtual void OnTargetStarted(TargetStartedEventArgs e, TProjectData projectData, TBuildData buildData) { }

    /// <summary>
    /// Called when a target finishes.
    /// </summary>
    protected virtual void OnTargetFinished(TargetFinishedEventArgs e, TProjectData projectData, TBuildData buildData) { }

    /// <summary>
    /// Called when a task starts.
    /// </summary>
    protected virtual void OnTaskStarted(TaskStartedEventArgs e, TProjectData projectData, TBuildData buildData) { }

    /// <summary>
    /// Called when a message is raised.
    /// </summary>
    protected virtual void OnMessageRaised(BuildMessageEventArgs e, TProjectData? projectData, TBuildData buildData) { }

    /// <summary>
    /// Called when a warning is raised.
    /// </summary>
    protected virtual void OnWarningRaised(BuildWarningEventArgs e, TProjectData? projectData, TBuildData buildData) { }

    /// <summary>
    /// Called when an error is raised.
    /// </summary>
    protected virtual void OnErrorRaised(BuildErrorEventArgs e, TProjectData? projectData, TBuildData buildData) { }

    #endregion
}