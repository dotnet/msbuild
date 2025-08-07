// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging;

/// <summary>
/// A forwarding logger that forwards events relevant to implementing the TerminalLogger from build nodes to the central nodes.
/// This logger
/// </summary>
public sealed partial class ForwardingTerminalLogger : IForwardingLogger
{
    /// <inheritdoc />
    public IEventRedirector? BuildEventRedirector { get; set; }
    /// <inheritdoc />
    public int NodeId { get; set; }

    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;
    /// <inheritdoc />
    public string? Parameters { get; set; }

    /// <inheritdoc />
    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        NodeId = nodeCount;
        Initialize(eventSource);
    }

    /// <inheritdoc />
    public void Initialize(IEventSource eventSource)
    {
        eventSource.BuildStarted += ForwardEventUnconditionally;
        eventSource.BuildFinished += ForwardEventUnconditionally;
        eventSource.ProjectStarted += ForwardEventUnconditionally;
        eventSource.ProjectFinished += ForwardEventUnconditionally;
        eventSource.TargetStarted += TrackRelevantTargets;
        eventSource.TargetFinished += ScrubOutputsFromIrrelevantTargets;
        eventSource.TaskStarted += TaskStarted;

        eventSource.MessageRaised += MessageRaised;
        eventSource.WarningRaised += ForwardEventUnconditionally;
        eventSource.ErrorRaised += ForwardEventUnconditionally;
        eventSource.StatusEventRaised += ForwardEvaluationEvents;

        if (eventSource is IEventSource3 eventSource3)
        {
            eventSource3.IncludeTaskInputs();
        }

        if (eventSource is IEventSource4 eventSource4)
        {
            eventSource4.IncludeEvaluationPropertiesAndItems();
        }
    }

    private void ForwardEvaluationEvents(object sender, BuildStatusEventArgs e)
    {
        if (e is ProjectEvaluationStartedEventArgs ||
            e is ProjectEvaluationFinishedEventArgs)
        {
            // Forward evaluation events unconditionally
            BuildEventRedirector?.ForwardEvent(e);
        }
    }


    /// <summary>
    /// some targets have tasks/items/properties that we need to get the details of, so we track that here so we're not flooding the central node with events.
    /// </summary>
    private readonly Dictionary<int, string> _targetIdsToTrackTasksOf = new();
    private readonly HashSet<int> _taskIdsToTrackOutputsOf = new();

    /// <summary>
    /// sets up some bookkeeping for per-target tracking on the nodes to help filter input to the central node
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TrackRelevantTargets(object sender, TargetStartedEventArgs e)
    {
        switch (e.TargetName)
        {
            case "CopyFilesToOutputDirectory":
                // The Copy task in this target is used to copy the main assembly to the output directory.
                _targetIdsToTrackTasksOf[e.BuildEventContext!.TargetId] = "Copy";
                break;
        }
        BuildEventRedirector?.ForwardEvent(e);
    }

    private static readonly HashSet<string> _targetsWeCareAbout = new([
        "InitializeSourceRootMappedPaths", // sourceroots from this set up relative path computation for outputs
        "GenerateNuspec", // used for nuget package output detection
        "PublishItemsOutputGroup" // used for publish output detection
    ], StringComparer.OrdinalIgnoreCase);

    private void ScrubOutputsFromIrrelevantTargets(object sender, TargetFinishedEventArgs e)
    {
        if (_targetsWeCareAbout.Contains(e.TargetName))
        {
            BuildEventRedirector?.ForwardEvent(e);
            return;
        }

        if (_targetIdsToTrackTasksOf.TryGetValue(e.BuildEventContext!.TargetId, out string? _))
        {
            // If the target is relevant because it has a task we care about, then the target finishing
            // means that we can clear that tracking
            _targetIdsToTrackTasksOf.Remove(e.BuildEventContext.TargetId);
        }

        e.TargetOutputs = null;
        BuildEventRedirector?.ForwardEvent(e);
    }

    public void ForwardEventUnconditionally(object sender, BuildEventArgs e)
    {
        BuildEventRedirector?.ForwardEvent(e);
    }

    public void TaskStarted(object sender, TaskStartedEventArgs e)
    {
        if (e.BuildEventContext is null)
        {
            return;
        }

        // The central node updates the 'in-flight' node status on the terminal
        // when a node starts the MSBuild task, so we need to forward these along so that behavior still works.
        if (e.TaskName == "MSBuild")
        {
            BuildEventRedirector?.ForwardEvent(e);
        }

        if (_targetIdsToTrackTasksOf.TryGetValue(e.BuildEventContext.TargetId, out string? taskName) && e.TaskName == taskName)
        {
            _taskIdsToTrackOutputsOf.Add(e.BuildEventContext.TaskId);
            // If the task is one we care about, forward the started event
            // so that the terminal logger can display it.
            BuildEventRedirector?.ForwardEvent(e);
        }
    }

    private HashSet<int> _targetIdsToTrackForOutputs = new();

    public void MessageRaised(object sender, BuildMessageEventArgs e)
    {
        if (e.BuildEventContext is null)
        {
            return;
        }

        if (e is TargetSkippedEventArgs targetSkippedEventArgs && _targetsWeCareAbout.Contains(targetSkippedEventArgs.TargetName))
        {
            // If the target is one we care about, forward the skipped event
            // so that the terminal logger can display it.
            _targetIdsToTrackForOutputs.Add(targetSkippedEventArgs.BuildEventContext!.TargetId);
            BuildEventRedirector?.ForwardEvent(targetSkippedEventArgs);
            return;
        }

        if (e is TaskParameterEventArgs taskParameterEventArgs)
        {
            // skipped target outputs
            if (taskParameterEventArgs.Kind == TaskParameterMessageKind.SkippedTargetOutputs
                && _targetIdsToTrackForOutputs.Contains(taskParameterEventArgs.BuildEventContext!.TargetId))
            {
                _targetIdsToTrackForOutputs.Remove(taskParameterEventArgs.BuildEventContext.TargetId);

                // forward skipped outputs to the central node so we can try to reconstruct some stuff.
                // we need to try to fake outputs from non-skipped Targets, so we need to look at target context ids?
                BuildEventRedirector?.ForwardEvent(taskParameterEventArgs);
                return;
            }
            // tracked Task outputs
            else if (taskParameterEventArgs.Kind == TaskParameterMessageKind.TaskOutput
                     && _taskIdsToTrackOutputsOf.Contains(taskParameterEventArgs.BuildEventContext!.TaskId))
            {
                _targetIdsToTrackTasksOf.Remove(taskParameterEventArgs.BuildEventContext.TargetId);
                _taskIdsToTrackOutputsOf.Remove(taskParameterEventArgs.BuildEventContext.TaskId);
                // If the task outputs are from a target we care about, forward them
                // so that the terminal logger can display them.
                BuildEventRedirector?.ForwardEvent(taskParameterEventArgs);
                return;
            }
        }

        if (e.Message is not null &&
            // Never forward messages if the verbosity is quiet
            Verbosity != LoggerVerbosity.Quiet &&
            // High-priority messages are always collected by the central node
            (e.Importance == MessageImportance.High
            // Normmal-priority messages are only collected if the verbosity is more verbose than normal
            || (e.Importance == MessageImportance.Normal && Verbosity > LoggerVerbosity.Normal)))
        {
            BuildEventRedirector?.ForwardEvent(e);
        }
    }

    /// <inheritdoc />
    public void Shutdown() {}
}
