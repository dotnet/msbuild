// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging;

/// <summary>
/// A forwarding logger that forwards events relevant to implementing the TerminalLogger from build nodes to the central nodes.
/// This logger
/// </summary>
public sealed partial class ForwardingTerminalLogger : IForwardingLogger
{
    private const string MSBuildTaskName = "MSBuild";
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
        eventSource.TargetStarted += ForwardEventUnconditionally;
        eventSource.TargetFinished += ScrubOutputsFromIrrelevantTargets;
        eventSource.TaskStarted += TaskStarted;
        eventSource.TaskFinished += TaskFinished;

        eventSource.MessageRaised += MessageRaised;
        eventSource.WarningRaised += ForwardEventUnconditionally;
        eventSource.ErrorRaised += ForwardEventUnconditionally;
        eventSource.StatusEventRaised += ForwardEvaluationEvents;

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

    private static HashSet<string> _targetsWeCareAbout = [
        "GetTargetPath",
        "InitializeSourceRootMappedPaths"
    ];

    private void ScrubOutputsFromIrrelevantTargets(object sender, TargetFinishedEventArgs e)
    {
        // If the target is not relevant to the terminal logger, scrub the outputs
        if (_targetsWeCareAbout.Contains(e.TargetName))
        {
            BuildEventRedirector?.ForwardEvent(e);
        }
        else
        {
            e.TargetOutputs = null;
            BuildEventRedirector?.ForwardEvent(e);
        }
    }

    public void ForwardEventUnconditionally(object sender, BuildEventArgs e)
    {
        BuildEventRedirector?.ForwardEvent(e);
    }

    public void TaskStarted(object sender, TaskStartedEventArgs e)
    {
        // The central node updates the 'in-flight' node status on the terminal
        // when a node starts the MSBuild task, so we need to forward these along so that behavior still works.
        if (e.BuildEventContext is not null && e.TaskName == MSBuildTaskName)
        {
            BuildEventRedirector?.ForwardEvent(e);
        }
    }

    public void TaskFinished(object sender, TaskFinishedEventArgs e)
    {
        // The central node updates the 'in-flight' node status on the terminal
        // when a node finishes the MSBuild task, so we need to forward these along so that behavior still works.
        if (e.BuildEventContext is not null && e.TaskName == MSBuildTaskName)
        {
            BuildEventRedirector?.ForwardEvent(e);
        }
    }

    public void MessageRaised(object sender, BuildMessageEventArgs e)
    {
        // Never forward messages if verbosity is quiet
        if (Verbosity == LoggerVerbosity.Quiet)
        {
            return;
        }

        // Never forward messages without content
        if (e.Message is null)
        {
            return;
        }

        // Never forward messages without context unless they have high importance
        // (high importance messages from coordinator/global context are still forwarded)
        if (e.BuildEventContext is null && e.Importance != MessageImportance.High)
        {
            return;
        }

        // Forward high-priority messages or normal-priority messages when verbosity is high enough
        if (e.Importance == MessageImportance.High ||
           (e.Importance == MessageImportance.Normal && Verbosity > LoggerVerbosity.Normal))
        {
            BuildEventRedirector?.ForwardEvent(e);
        }
    }

    /// <inheritdoc />
    public void Shutdown() {}
}
