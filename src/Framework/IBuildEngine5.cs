// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

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