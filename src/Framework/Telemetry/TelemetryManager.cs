// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using Microsoft.VisualStudio.Telemetry;
#endif

using System;
using System.Collections.Generic;
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
        internal const string ReleaseCanaryEnvironmentVariable = "MSBUILD_TELEMETRY_CANARY_ID";

        /// <summary>
        /// Lock object for thread-safe initialization and disposal.
        /// </summary>
        private static readonly LockType s_lock = new();

        private static bool s_initialized;
        private static bool s_disposed;

        /// <summary>
        /// Indicates whether the telemetry infrastructure has already been torn down.
        /// Exposed for TESTING purposes.
        /// </summary>
        internal static bool IsDisposed => s_disposed;

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
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // Telemetry is best effort and must never fail a build.
                // Microsoft.VisualStudio.Telemetry or System.Diagnostics.DiagnosticSource might not be available outside of VS or dotnet
                // (FileNotFoundException, FileLoadException, TypeLoadException) - this is expected in standalone application scenarios
                // (when MSBuild.exe is invoked directly). The telemetry stack itself can also throw, for example when the machine is
                // configured to opt out of Visual Studio telemetry, so any non-critical failure simply disables telemetry for this process.
                DefaultActivitySource = null;
            }
        }

        internal void EmitReleaseCanary(string? canaryId, string buildEngineVersion)
        {
#if NETFRAMEWORK
            lock (s_lock)
            {
                if (s_disposed)
                {
                    return;
                }

                try
                {
                    VsTelemetryInitializer.PostReleaseCanary(canaryId, buildEngineVersion);
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    // The release pipeline verifies ingestion externally. Telemetry must remain best effort
                    // inside MSBuild even when the canary cannot be posted.
                }
            }
#endif
        }

        internal static Dictionary<string, object> CreateReleaseCanaryProperties(Guid canaryId, string buildEngineVersion) =>
            new()
            {
                [$"{TelemetryConstants.PropertyPrefix}CanaryId"] = canaryId.ToString("N"),
                [$"{TelemetryConstants.PropertyPrefix}BuildEngineVersion"] = buildEngineVersion,
            };

        public void Dispose()
        {
            lock (s_lock)
            {
                if (s_disposed)
                {
                    return;
                }

                s_disposed = true;

                // Nothing may use the activity source once the underlying session is gone.
                DefaultActivitySource = null;

#if NETFRAMEWORK
                try
                {
                    DisposeVsTelemetry();
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    // Telemetry is best effort and must never fail a build.
                    // The Visual Studio telemetry assembly may never have been loaded (FileNotFoundException,
                    // FileLoadException, TypeLoadException), and disposing the session can itself throw when the
                    // telemetry stack was not fully started - for example when telemetry is opted out machine wide.
                    // Critical exceptions still propagate because the process is not safe to continue.
                }
#endif
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
            TelemetrySession? session;
            if (isStandalone)
            {
                session = TelemetryService.CreateAndGetDefaultSession(CollectorApiKey);
            }
            else
            {
                session = TelemetryService.DefaultSession;
            }

            // Record ownership before configuring the session so shutdown can clean up even if the
            // telemetry stack throws partway through initialization.
            s_telemetrySession = session;
            s_ownsSession = isStandalone && session is not null;

            // The telemetry stack can decline to create a session, for example when telemetry is disabled
            // machine wide.
            if (isStandalone && session is not null)
            {
                session.UseVsIsOptedIn();
                session.Start();
            }

            return new MSBuildActivitySource(session);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Dispose()
        {
            object? telemetrySession = s_telemetrySession;
            bool ownsSession = s_ownsSession;

            // Clear the state first so that a failure while disposing the session cannot leave a stale
            // session behind, and so that a subsequent disposal attempt is a no-op.
            s_telemetrySession = null;
            s_ownsSession = false;

            if (ownsSession && telemetrySession is TelemetrySession session)
            {
                session.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void PostReleaseCanary(string? canaryId, string buildEngineVersion)
        {
            if (s_telemetrySession is not TelemetrySession session ||
                !Guid.TryParseExact(canaryId, "N", out Guid parsedCanaryId))
            {
                return;
            }

            session.PostEvent(CreateReleaseCanaryEvent(parsedCanaryId, buildEngineVersion));
        }

        internal static TelemetryEvent CreateReleaseCanaryEvent(Guid canaryId, string buildEngineVersion)
        {
            TelemetryEvent telemetryEvent = new($"{TelemetryConstants.EventPrefix}{TelemetryConstants.ReleaseCanary}");
            foreach (KeyValuePair<string, object> property in TelemetryManager.CreateReleaseCanaryProperties(canaryId, buildEngineVersion))
            {
                telemetryEvent.Properties[property.Key] = property.Value;
            }

            return telemetryEvent;
        }
    }
#endif
}
