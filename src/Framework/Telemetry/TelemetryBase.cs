// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework.Telemetry;

internal abstract class TelemetryBase
{
    /// <summary>
    /// Gets or sets the name of the event.
    /// </summary>
    public abstract string EventName { get; }

    /// <summary>
    /// Gets or sets a list of properties associated with the event.
    /// </summary>
    private protected IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Fetch current properties <see cref="Properties"/>.
    /// </summary>
    public abstract IDictionary<string, string> GetProperties();

    /// <summary>
    /// Translate all derived type members into properties which will be used to build <see cref="TelemetryEventArgs"/>.
    /// </summary>
    public abstract void UpdateEventProperties();
}
