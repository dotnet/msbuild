// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using System.Collections.Generic;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Represents a Visual Studio telemetry activity that wraps a <see cref="TelemetryScope{T}"/>.
    /// This class provides an implementation of <see cref="IActivity"/> for the VS Telemetry system,
    /// allowing telemetry data to be collected and sent when running on .NET Framework.
    /// </summary>
    internal class VsTelemetryActivity : IActivity
    {
        private readonly TelemetryScope<OperationEvent> _scope;
        private TelemetryResult _result = TelemetryResult.Success;

        private bool _disposed;

        public VsTelemetryActivity(TelemetryScope<OperationEvent> scope)
        {
            _scope = scope;
        }

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

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _scope.End(_result);
            _disposed = true;
        }
    }
}

#endif
