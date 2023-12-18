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
    /// Fetches current properties wrapped in Dictionary
    /// </summary>
    public abstract IDictionary<string, string> GetProperties();
}
