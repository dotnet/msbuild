// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NETFRAMEWORK

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Wraps a <see cref="Activity"/> and implements <see cref="IActivity"/>.
    /// </summary>
    internal class DiagnosticActivity : IActivity
    {
        private readonly Activity _activity;
        private bool _disposed;

        public DiagnosticActivity(Activity activity)
        {
            _activity = activity;
        }

        public IActivity? SetTags(IActivityTelemetryDataHolder? dataHolder)
        {
            Dictionary<string, object>? tags = dataHolder?.GetActivityProperties();
            if (tags != null)
            {
                foreach (KeyValuePair<string, object> tag in tags)
                {
                    SetTag(tag.Key, tag.Value);
                }
            }

            return this;
        }

        public IActivity? SetTag(string key, object? value)
        {
            if (value != null)
            {
                _activity.SetTag($"{TelemetryConstants.PropertyPrefix}{key}", value);
            }

            return this;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _activity.Dispose();

            _disposed = true;
        }
    }
}

#endif
