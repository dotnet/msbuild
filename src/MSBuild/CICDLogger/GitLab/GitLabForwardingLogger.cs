// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.CommandLine.CICDLogger.GitLab;

/// <summary>
/// Forwarding logger for GitLab CI that filters and forwards events from build nodes to the central logger.
/// </summary>
public sealed class GitLabForwardingLogger : IForwardingLogger
{
    /// <inheritdoc/>
    public IEventRedirector? BuildEventRedirector { get; set; }

    /// <inheritdoc/>
    public int NodeId { get; set; }

    /// <inheritdoc/>
    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

    /// <inheritdoc/>
    public string? Parameters { get; set; }

    /// <inheritdoc/>
    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        NodeId = nodeCount;
        Initialize(eventSource);
    }

    /// <inheritdoc/>
    public void Initialize(IEventSource eventSource)
    {
        // Forward all errors and warnings unconditionally
        eventSource.ErrorRaised += ForwardEvent;
        eventSource.WarningRaised += ForwardEvent;

        // Forward build lifecycle events
        eventSource.BuildStarted += ForwardEvent;
        eventSource.BuildFinished += ForwardEvent;
        eventSource.ProjectStarted += ForwardEvent;
        eventSource.ProjectFinished += ForwardEvent;

        // Forward messages based on importance and verbosity
        eventSource.MessageRaised += MessageRaised;
    }

    /// <inheritdoc/>
    public void Shutdown()
    {
    }

    private void ForwardEvent(object sender, BuildEventArgs e)
    {
        BuildEventRedirector?.ForwardEvent(e);
    }

    private void MessageRaised(object sender, BuildMessageEventArgs e)
    {
        // Forward messages based on verbosity
        if (Verbosity == LoggerVerbosity.Quiet)
        {
            return;
        }

        if (e.Importance == MessageImportance.High ||
            (e.Importance == MessageImportance.Normal && Verbosity >= LoggerVerbosity.Normal) ||
            (e.Importance == MessageImportance.Low && Verbosity >= LoggerVerbosity.Detailed))
        {
            BuildEventRedirector?.ForwardEvent(e);
        }
    }
}
