// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends IBuildEngine to log telemetry.
    /// </summary>
    public interface IBuildEngine5 : IBuildEngine4
    {
        /// <summary>
        /// Logs telemetry.
        /// </summary>
        /// <param name="eventName">The event name.</param>
        /// <param name="properties">The event properties.</param>
        void LogTelemetry(string eventName, IDictionary<string, string> properties);
    }
}
