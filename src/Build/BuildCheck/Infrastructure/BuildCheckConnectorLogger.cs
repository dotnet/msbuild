// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// Central logger for the build check infrastructure.
/// Receives events from the <see cref="BuildCheckForwardingLogger"/>.
/// Processes the events and forwards them to the <see cref="IBuildCheckManager"/> and registered checks.
/// </summary>
/// <remarks>
/// Ensure that the consuming events are in sync with <see cref="BuildCheckForwardingLogger"/>.
/// </remarks>
internal sealed class BuildCheckConnectorLogger : ILogger
{
    private readonly BuildCheckBuildEventHandler _eventHandler;

    internal BuildCheckConnectorLogger(
        ICheckContextFactory checkContextFactory,
        IBuildCheckManager buildCheckManager)
    {
        _eventHandler = new BuildCheckBuildEventHandler(checkContextFactory, buildCheckManager);
    }

    public LoggerVerbosity Verbosity { get; set; }

    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
        eventSource.AnyEventRaised += EventSource_AnyEventRaised;

        if (eventSource is IEventSource3 eventSource3)
        {
            eventSource3.IncludeTaskInputs();
        }

        if (eventSource is IEventSource4 eventSource4)
        {
            eventSource4.IncludeEvaluationPropertiesAndItems();
        }
    }

    public void Shutdown()
    {
    }

    private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
        => _eventHandler.HandleBuildEvent(e);
}
