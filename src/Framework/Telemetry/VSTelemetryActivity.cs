// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.Build.Framework.Telemetry
{
    internal class VsTelemetryActivity : IActivity
    {
        private readonly TelemetryScope<OperationEvent> _scope;
        private TelemetryResult _result = TelemetryResult.Success;
        private string? _resultSummary;

        private bool _disposed;

        public VsTelemetryActivity(TelemetryScope<OperationEvent> scope) => _scope = scope;

        public IActivity? SetTags(IActivityTelemetryDataHolder? dataHolder)
        {
            Dictionary<string, object>? tags = dataHolder?.GetActivityProperties();

            if (tags != null)
            {
                foreach (KeyValuePair<string, object> tag in tags)
                {
                    _ = SetTag(tag.Key, tag.Value);
                }
            }

            return this;
        }

        public IActivity? SetTag(string key, object? value)
        {
            if (value != null)
            {
                _scope.EndEvent.Properties[$"{TelemetryConstants.PropertyPrefix}{key}"] = new TelemetryComplexProperty(value);
            }

            return this;
        }
        public IActivity? AddEvent(ActivityEvent activityEvent)
        {
            // VS Telemetry doesn't have a direct equivalent to ActivityEvent
            // We could create and immediately post a custom event if needed.
            var telemetryEvent = new TelemetryEvent(activityEvent.Name);
            foreach (KeyValuePair<string, object?> tag in activityEvent.Tags)
            {
                telemetryEvent.Properties[$"{TelemetryConstants.PropertyPrefix}{tag.Key}"] = tag.Value;
            }

            TelemetryService.DefaultSession.PostEvent(telemetryEvent);
            return this;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // End the operation
            _scope.End(_result, _resultSummary);
            _disposed = true;
        }
    }
}

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

    /// <summary>
    /// Adds an event to the activity.
    /// </summary>
    /// <param name="activityEvent">The event to add.</param>
    /// <returns>The activity instance for method chaining.</returns>
    IActivity? AddEvent(ActivityEvent activityEvent);
}

#endif
