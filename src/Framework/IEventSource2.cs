// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
