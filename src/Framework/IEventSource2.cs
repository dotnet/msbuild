// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Type of handler for TelemetryLogged events
    /// </summary>
    public delegate void TelemetryEventHandler(object sender, TelemetryEventArgs e);

    /// <summary>
    /// This interface defines the events raised by the build engine.
    /// Loggers use this interface to subscribe to the events they
    /// are interested in receiving.
    /// </summary>
    public interface IEventSource2 : IEventSource
    {
        /// <summary>
        /// this event is raised to when telemetry is logged.
        /// </summary>
        event TelemetryEventHandler TelemetryLogged;
    }
}
