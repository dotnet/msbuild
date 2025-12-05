// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// VS Telemetry implementation. This class is in a separate file to ensure
    /// the Microsoft.VisualStudio.Telemetry assembly is only loaded when this type is accessed.
    /// </summary>
    internal sealed class VsTelemetrySession : ITelemetrySession
    {
        private const string CollectorApiKey = "f3e86b4023cc43f0be495508d51f588a-f70d0e59-0fb0-4473-9f19-b4024cc340be-7296";

        private readonly TelemetrySession _session;
        private bool _disposed;
        private readonly bool _ownsSession;

        private VsTelemetrySession(TelemetrySession session, bool ownsSession)
        {
            _session = session;
            _ownsSession = ownsSession;
        }

        public static ITelemetrySession Create(bool isStandalone)
        {
            if (isStandalone)
            {
                TelemetrySession session = TelemetryService.CreateAndGetDefaultSession(CollectorApiKey);
                TelemetryService.DefaultSession.IsOptedIn = true;
                TelemetryService.DefaultSession.Start();
                return new VsTelemetrySession(session, ownsSession: true);
            }

            return new VsTelemetrySession(TelemetryService.DefaultSession, ownsSession: false);
        }

        public IActivity? StartActivity(string name)
        {
            string eventName = $"{TelemetryConstants.EventPrefix}{name}";
            TelemetryScope<OperationEvent>? operation = _session.StartOperation(eventName);
            return operation != null ? new VsTelemetryActivity(operation) : null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_ownsSession)
            {
                _session.Dispose();
            }

            _disposed = true;
        }
    }
}
#endif
