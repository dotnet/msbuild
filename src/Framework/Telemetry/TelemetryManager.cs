// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
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
        private static readonly LockType s_lock = new();

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

        /// <summary>
        /// Initializes the telemetry manager with the specified configuration.
        /// </summary>
        /// <param name="isStandalone">
        /// Indicates whether MSBuild is running in standalone mode (e.g., MSBuild.exe directly invoked)
        /// versus integrated mode (e.g., running within Visual Studio or dotnet CLI).
        /// When <c>true</c>, creates and manages its own telemetry session on .NET Framework.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
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
        /// Resets the TelemetryManager state for TESTING purposes.
        /// </summary>
        internal static void ResetForTest()
        {
            lock (s_lock)
            {
                s_initialized = false;
                s_disposed = false;
                Instance.DefaultActivitySource = null;
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
                // This is expected in standalone application scenarios (when MSBuild.exe is invoked directly).
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

        /// <summary>
        /// Determines if the user has explicitly opted out of telemetry.
        /// </summary>
        internal static bool IsOptOut() =>
#if NETFRAMEWORK
            Traits.Instance.FrameworkTelemetryOptOut;
#else
            Traits.Instance.SdkTelemetryOptOut;
#endif

#if NETFRAMEWORK
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DisposeVsTelemetry() => VsTelemetryInitializer.Dispose();
#endif
    }

#if NETFRAMEWORK
    internal static class VsTelemetryInitializer
    {
        // Telemetry API key for Visual Studio telemetry service.
        private const string CollectorApiKey = "f3e86b4023cc43f0be495508d51f588a-f70d0e59-0fb0-4473-9f19-b4024cc340be-7296";

        // Store as object to avoid type reference at class load time
        private static object? s_telemetrySession;
        private static bool s_ownsSession = false;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static MSBuildActivitySource Initialize(bool isStandalone)
        {
            TelemetrySession session;
            if (isStandalone)
            {
                session = TelemetryService.CreateAndGetDefaultSession(CollectorApiKey);
                TelemetryService.DefaultSession.UseVsIsOptedIn();
                TelemetryService.DefaultSession.Start();
                s_ownsSession = true;
            }
            else
            {
                session = TelemetryService.DefaultSession;
            }

            s_telemetrySession = session;
            return new MSBuildActivitySource(session);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Dispose()
        {
            if (s_ownsSession && s_telemetrySession is TelemetrySession session)
            {
                session.Dispose();
            }

            s_telemetrySession = null;
        }
    }
#endif
}
