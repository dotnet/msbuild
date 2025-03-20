// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Telemetry;

/// <remarks>
/// Ensure that events filtering is in sync with InternalTelemetryConsumingLogger.
/// </remarks>
internal class InternalTelemetryForwardingLogger : IForwardingLogger
{
    public IEventRedirector? BuildEventRedirector { get; set; }

    public int NodeId { get; set; }

    public LoggerVerbosity Verbosity { get => LoggerVerbosity.Quiet; set { return; } }

    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource, int nodeCount) => Initialize(eventSource);

    public void Initialize(IEventSource eventSource)
    {
        if (BuildEventRedirector != null && eventSource is IEventSource5 eventSource5)
        {
            eventSource5.WorkerNodeTelemetryLogged += (o,e) => BuildEventRedirector.ForwardEvent(e);
        }
    }

    public void Shutdown()
    {
    }
}
