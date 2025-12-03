// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using Microsoft.VisualStudio.Telemetry;
#endif

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Manages telemetry collection and reporting for MSBuild.
    /// This class provides a centralized way to initialize, configure, and manage telemetry sessions.
    /// </summary>
    /// <remarks>
    /// The TelemetryManager is a singleton that handles both standalone and integrated telemetry scenarios.
    /// On .NET Framework, it integrates with Visual Studio telemetry services.
    /// On .NET Core it provides a lightweight telemetry implementation though exposing an activity source.
    /// </remarks>
    internal class TelemetryManager
    {
#if NETFRAMEWORK
        private const string CollectorApiKey = "f3e86b4023cc43f0be495508d51f588a-f70d0e59-0fb0-4473-9f19-b4024cc340be-7296";

        private static TelemetrySession? _telemetrySession;
#endif
        private static bool s_disposed;

        private TelemetryManager()
        {
        }

        /// <summary>
        /// Optional activity source for MSBuild or other telemetry usage.
        /// </summary>
        public MSBuildActivitySource? DefaultActivitySource { get; private set; }

        public static TelemetryManager Instance { get; } = new TelemetryManager();

        public void Initialize(bool isStandalone)
        {
            if (IsOptOut())
            {
                return;
            }

#if NETFRAMEWORK
            if (_telemetrySession != null)
            {
                return;
            }

            if (isStandalone)
            {
                _telemetrySession = TelemetryService.CreateAndGetDefaultSession(CollectorApiKey);
                TelemetryService.DefaultSession.IsOptedIn = true;
                TelemetryService.DefaultSession.Start();
            }
            else
            {
                _telemetrySession = TelemetryService.DefaultSession;
            }

            DefaultActivitySource = new MSBuildActivitySource(TelemetryConstants.DefaultActivitySourceNamespace, _telemetrySession);
#else
            DefaultActivitySource = new MSBuildActivitySource(TelemetryConstants.DefaultActivitySourceNamespace);
#endif
        }

        public void Dispose()
        {
            if (s_disposed)
            {
                return;
            }

#if NETFRAMEWORK
            _telemetrySession?.Dispose();
#endif
            s_disposed = true;
        }

        /// <summary>
        /// Determines if the user has explicitly opted out of telemetry.
        /// </summary>
        private bool IsOptOut() => Traits.Instance.FrameworkTelemetryOptOut || Traits.Instance.SdkTelemetryOptOut;
    }
}
