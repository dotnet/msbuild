// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using Microsoft.VisualStudio.Telemetry;
#else
using System.Diagnostics;
#endif

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Wrapper class for ActivitySource with a <see cref="StartActivity(string)"/> method that wraps Activity name with MSBuild prefix.
    /// On .NET Framework, activities are also forwarded to VS Telemetry.
    /// </summary>
    internal class MSBuildActivitySource
    {
#if NETFRAMEWORK
        private readonly TelemetrySession? _telemetrySession;

        public MSBuildActivitySource(TelemetrySession? telemetrySession)
        {
            _telemetrySession = telemetrySession;
            IsTelemetryEnabled = _telemetrySession?.IsOptedIn ?? false;
        }
#else
        private readonly ActivitySource _source;

        public MSBuildActivitySource(string name, bool isTelemetryExplicitlyRequested)
        {
            _source = new ActivitySource(name);
            IsTelemetryEnabled = isTelemetryExplicitlyRequested;
        }
#endif

        /// <summary>
        /// Gets a value indicating whether telemetry is enabled for this activity source.
        /// On .NET Framework, this reflects whether the VS Telemetry session is opted in.
        /// On .NET Core and later, this reflects whether telemetry was explicitly requested.
        /// </summary>
        public bool IsTelemetryEnabled { get; }

        /// <summary>
        /// Starts a new activity with the appropriate telemetry prefix.
        /// </summary>
        /// <param name="name">Name of the telemetry event without prefix.</param>
        /// <returns>An <see cref="IActivity"/> wrapping the underlying Activity, or null if not sampled.</returns>
        public IActivity? StartActivity(string name)
        {
            string eventName = $"{TelemetryConstants.EventPrefix}{name}";

#if NETFRAMEWORK
            TelemetryScope<OperationEvent>? operation = _telemetrySession?.StartOperation(eventName);
            return operation != null ? new VsTelemetryActivity(operation) : null;
#else
            Activity? activity = Activity.Current?.HasRemoteParent == true
                ? _source.StartActivity(eventName, ActivityKind.Internal, parentId: Activity.Current.ParentId)
                : _source.StartActivity(eventName);

            if (activity == null)
            {
                return null;
            }

            activity.SetTag("SampleRate", TelemetryConstants.DefaultSampleRate);

            return new DiagnosticActivity(activity);
#endif
        }
    }
}
