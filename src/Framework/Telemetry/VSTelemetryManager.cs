// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.Build.Framework.Telemetry
{
    internal class VSTelemetryManager
    {
        private const string CollectorApiKey = "0c6ae279ed8443289764825290e4f9e2-1a736e7c-1324-4338-be46-fc2a58ae4d14-7255";

        private static TelemetrySession? _telemetrySession;

        private static bool _disposed;

        public VSTelemetryManager(bool isStandalone) => Initialize(isStandalone);

        private void Initialize(bool isStandalone)
        {
            if (isStandalone)
            {
                _telemetrySession = TelemetryService.CreateAndGetDefaultSession(CollectorApiKey);
                TelemetryService.DefaultSession.IsOptedIn = true;

                // Start session, so we can start sending events
                TelemetryService.DefaultSession.Start();

                return;
            }

            _telemetrySession = TelemetryService.DefaultSession;
        }

        public static VsTelemetryActivity? StartActivity(string name)
        {
            string eventName = $"{TelemetryConstants.EventPrefix}{name}";
            TelemetryScope<OperationEvent>? operation = _telemetrySession.StartOperation(eventName);

            return operation != null ? new VsTelemetryActivity(operation) : null;
        }

        public static void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _telemetrySession?.Dispose();

            _disposed = true;
        }
    }
}

#endif
