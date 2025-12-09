// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Telemetry;
#endif

using System;

using System.IO;
using System.Runtime.CompilerServices;

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
        /// <summary>
        /// Lock object for thread-safe initialization and disposal.
        /// </summary>
        private static readonly object s_lock = new object();

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
            lock (s_lock)
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

                TryInitializeTelemetry(isStandalone);
            }
        }

        /// <summary>
        /// Initializes MSBuild telemetry.
        /// This method is deliberately not inlined to ensure
        /// the Telemetry related assemblies are only loaded when this method is called,
        /// allowing the calling code to catch assembly loading exceptions.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TryInitializeTelemetry(bool isStandalone)
        {
            try
            {
#if NETFRAMEWORK
                DefaultActivitySource = VsTelemetryInitializer.Initialize(isStandalone);
#else
                DefaultActivitySource = new MSBuildActivitySource(TelemetryConstants.DefaultActivitySourceNamespace);
#endif
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or TypeLoadException)
            {
                // Microsoft.VisualStudio.Telemetry or System.Diagnostics.DiagnosticSource might not be available outside of VS or dotnet.
                // This is expected in standalone application scenarios.
                DefaultActivitySource = null;
            }
        }

        public void Dispose()
        {
            lock (s_lock)
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
        }

#if NETFRAMEWORK
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DisposeVsTelemetry() => VsTelemetryInitializer.Dispose();
#endif

        /// <summary>
        /// Determines if the user has explicitly opted out of telemetry.
        /// </summary>
        private static bool IsOptOut() =>
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
    /// <remarks>
    /// Thread-safety: All public methods on this class must be called under the
    /// <see cref="TelemetryManager"/> lock to ensure thread-safe access to static state.
    /// Callers must not invoke <see cref="Initialize"/> or <see cref="Dispose"/> concurrently.
    /// </remarks>
    internal static class VsTelemetryInitializer
    {
        // Telemetry API key for Visual Studio telemetry service.
        private const string CollectorApiKey = "f3e86b4023cc43f0be495508d51f588a-f70d0e59-0fb0-4473-9f19-b4024cc340be-7296";

        private static TelemetrySession? s_telemetrySession;
        private static bool s_ownsSession;

        public static MSBuildActivitySource Initialize(bool isStandalone)
        {
            if (isStandalone)
            {
                s_telemetrySession = TelemetryService.CreateAndGetDefaultSession(CollectorApiKey);
                TelemetryService.DefaultSession.IsOptedIn = true;
                TelemetryService.DefaultSession.Start();
                s_ownsSession = true;
            }
            else
            {
                s_telemetrySession = TelemetryService.DefaultSession;
                s_ownsSession = false;
            }

            return new MSBuildActivitySource(s_telemetrySession);
        }

        public static void Dispose()
        {
            // Only dispose the session if we created it (standalone scenario).
            // In VS, the session is owned by VS and should not be disposed by MSBuild.
            if (s_ownsSession)
            {
                s_telemetrySession?.Dispose();
            }

            s_telemetrySession = null;
        }
    }
#endif
}
