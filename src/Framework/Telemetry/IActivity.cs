// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Represents an activity for telemetry tracking.
    /// </summary>
    internal interface IActivity : IDisposable
    {
        /// <summary>
        /// Sets a tag on the activity.
        /// </summary>
        /// <param name="dataHolder">Telemetry data holder.</param>
        /// <returns>The activity instance for method chaining.</returns>
        IActivity? SetTags(IActivityTelemetryDataHolder? dataHolder);

        /// <summary>
        /// Sets a tag on the activity.
        /// </summary>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The tag value.</param>
        /// <returns>The activity instance for method chaining.</returns>
        IActivity? SetTag(string key, object? value);
    }
}
