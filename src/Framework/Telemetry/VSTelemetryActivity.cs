// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.Build.Framework.Telemetry
{
    internal class VsTelemetryActivity
    {
        private readonly TelemetryScope<OperationEvent> _scope;
        private TelemetryResult _result = TelemetryResult.Success;
        private string? _resultSummary;

        private bool _disposed;

        public VsTelemetryActivity(TelemetryScope<OperationEvent> scope)
        {
            _scope = scope;
        }

        public VsTelemetryActivity? AddComplexProperty(string key, TelemetryComplexProperty complexProperty)
        {
            _scope.EndEvent.Properties[key] = complexProperty;

            return this;
        }

        public VsTelemetryActivity? AddTag(string key, string? value)
        {
            _scope.EndEvent.Properties[key] = value;
            return this;
        }

        public VsTelemetryActivity? SetTag(string key, object? value)
        {
            _scope.EndEvent.Properties[key] = value;
            return this;
        }

        public VsTelemetryActivity? SetStatus(ActivityStatusCode status, string? description = null)
        {
            // Map ActivityStatusCode to TelemetryResult
            _result = status switch
            {
                ActivityStatusCode.Ok => TelemetryResult.Success,
                ActivityStatusCode.Error => TelemetryResult.Failure,
                _ => TelemetryResult.None,
            };

            _resultSummary = description;

            return this;
        }

        public VsTelemetryActivity? AddEvent(ActivityEvent activityEvent)
        {
            // VS Telemetry doesn't have a direct equivalent to ActivityEvent
            // We could create and immediately post a custom event if needed.
            var telemetryEvent = new TelemetryEvent(activityEvent.Name);
            foreach (KeyValuePair<string, object?> tag in activityEvent.Tags)
            {
                telemetryEvent.Properties[tag.Key] = tag.Value;
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
#endif
