// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;
using System.IO;
using System.Runtime.CompilerServices;
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
    /// On .NET Core it provides a lightweight telemetry implementation through exposing an activity source.
    /// </remarks>
    internal class TelemetryManager
    {
        private static bool s_initialized;
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
            if (s_initialized)
            {
                return;
            }

            s_initialized = true;

            if (IsOptOut())
            {
                return;
            }

#if NETFRAMEWORK
            try
            {
                InitializeVsTelemetry(isStandalone);
            }
            catch (Exception ex) when (
                ex is FileNotFoundException or
                FileLoadException or
                TypeLoadException)
            {
                // Microsoft.VisualStudio.Telemetry is not available outside VS.
                // This is expected in standalone MSBuild.exe scenarios.
                DefaultActivitySource = null;
            }
#else
            DefaultActivitySource = new MSBuildActivitySource(TelemetryConstants.DefaultActivitySourceNamespace);
#endif
        }

#if NETFRAMEWORK
        /// <summary>
        /// Initializes Visual Studio telemetry.
        /// This method is deliberately not inlined to ensure
        /// the Microsoft.VisualStudio.Telemetry assembly is only loaded when this method is called,
        /// allowing the calling code to catch assembly loading exceptions.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InitializeVsTelemetry(bool isStandalone) => DefaultActivitySource = VsTelemetryInitializer.Initialize(isStandalone);
#endif

        public void Dispose()
        {
            if (s_disposed)
            {
                return;
            }

#if NETFRAMEWORK
            try
            {
                DisposeVsTelemetry();
            }
            catch (Exception ex) when (
                ex is FileNotFoundException or
                FileLoadException or
                TypeLoadException)
            {
                // Assembly was never loaded, nothing to dispose.
            }
#endif
            s_disposed = true;
        }

#if NETFRAMEWORK
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DisposeVsTelemetry() => VsTelemetryInitializer.Dispose();
#endif

        /// <summary>
        /// Determines if the user has explicitly opted out of telemetry.
        /// </summary>
        private bool IsOptOut() =>
#if NETFRAMEWORK
            Traits.Instance.FrameworkTelemetryOptOut;
#else
            Traits.Instance.SdkTelemetryOptOut;
#endif
    }

#if NETFRAMEWORK
    /// <summary>
    /// Isolated class that references Microsoft.VisualStudio.Telemetry types.
    /// This separation ensures the VS Telemetry assembly is only loaded when methods
    /// on this class are actually invoked.
    /// </summary>
    internal static class VsTelemetryInitializer
    {
        // Telemetry API key for Visual Studio telemetry service.
        private const string CollectorApiKey = "f3e86b4023cc43f0be495508d51f588a-f70d0e59-0fb0-4473-9f19-b4024cc340be-7296";

        private static TelemetrySession? _telemetrySession;
        private static bool _ownsSession;

        public static MSBuildActivitySource Initialize(bool isStandalone)
        {
            if (isStandalone)
            {
                _telemetrySession = TelemetryService.CreateAndGetDefaultSession(CollectorApiKey);
                TelemetryService.DefaultSession.IsOptedIn = true;
                TelemetryService.DefaultSession.Start();
                _ownsSession = true;
            }
            else
            {
                _telemetrySession = TelemetryService.DefaultSession;
                _ownsSession = false;
            }

            return new MSBuildActivitySource(_telemetrySession);
        }

        public static void Dispose()
        {
            // Only dispose the session if we created it (standalone scenario).
            // In VS, the session is owned by VS and should not be disposed by MSBuild.
            if (_ownsSession)
            {
                _telemetrySession?.Dispose();
            }

            _telemetrySession = null;
        }
    }
#endif
}
