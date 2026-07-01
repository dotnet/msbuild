// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using Microsoft.Build.Framework.Telemetry;

namespace Microsoft.Build.Framework
{

    /// <summary>
    /// Type of handler for internal telemetry from worker node
    /// </summary>
    internal delegate void WorkerNodeTelemetryEventHandler(object? sender, WorkerNodeTelemetryEventArgs e);

    internal interface IEventSource5 : IEventSource4
    {
        /// <summary>
        /// this event is raised to when internal telemetry from worker node is logged.
        /// </summary>
        event WorkerNodeTelemetryEventHandler WorkerNodeTelemetryLogged;
    }
}
