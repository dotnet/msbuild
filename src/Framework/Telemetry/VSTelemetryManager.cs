// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.Build.Framework.Telemetry
{
    internal class VSTelemetryManager
    {
        private const string CollectorApiKey = "f3e86b4023cc43f0be495508d51f588a-f70d0e59-0fb0-4473-9f19-b4024cc340be-7296";

        private static TelemetrySession? _telemetrySession;

        private static bool _disposed;

        public VSTelemetryManager(bool isStandalone) => Initialize(isStandalone);

        private void Initialize(bool isStandalone)
        {
            if (_telemetrySession != null)
            {
                return;
            }

            if (isStandalone)
            {
                _telemetrySession = TelemetryService.CreateAndGetDefaultSession(CollectorApiKey);
                TelemetryService.DefaultSession.IsOptedIn = true;
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
