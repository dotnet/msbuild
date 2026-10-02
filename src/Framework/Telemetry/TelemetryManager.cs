// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Telemetry;
#endif

using System;
using System.IO;
using System.Reflection;
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
        internal const string ShutdownTimeoutEnvironmentVariable = "MSBUILD_TELEMETRY_SHUTDOWN_TIMEOUT_MS";
        internal const string DiagnosticsEnvironmentVariable = "MSBUILD_TELEMETRY_DIAGNOSTICS";

        internal const string OptOutEnvironmentVariable =
#if NETFRAMEWORK
            "MSBUILD_TELEMETRY_OPTOUT";
#else
            "DOTNET_CLI_TELEMETRY_OPTOUT";
#endif

        internal static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(10);

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
                // A session created after Dispose would never be shut down.
                if (s_initialized || s_disposed)
                {
                    return;
                }

                s_initialized = true;

                if (IsOptOut())
                {
                    WriteDiagnostic($"disabled by {OptOutEnvironmentVariable}.");
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
                WriteDiagnostic("initialized activity source.");
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
                WriteDiagnostic($"initialization failed, telemetry is disabled for this process: {DescribeException(ex)}.");
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

                s_disposed = true;

                // Nothing may use the activity source once the underlying session is gone.
                DefaultActivitySource = null;
            }

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
                WriteDiagnostic($"shutdown failed: {DescribeException(ex)}.");
            }
#endif
        }

        internal static TimeSpan GetShutdownTimeout()
        {
            int milliseconds = EnvironmentUtilities.GetValueAsInt32OrDefault(ShutdownTimeoutEnvironmentVariable, -1);
            return milliseconds >= 0 ? TimeSpan.FromMilliseconds(milliseconds) : DefaultShutdownTimeout;
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

        internal static bool IsDiagnosticsEnabled() => EnvironmentUtilities.IsValueOneOrTrue(DiagnosticsEnvironmentVariable);

        /// <summary>
        /// Writes a message to the standard error stream when diagnostics are enabled. Messages must not contain paths, user data, or event contents.
        /// </summary>
        internal static void WriteDiagnostic(string message)
        {
            try
            {
                if (IsDiagnosticsEnabled())
                {
                    Console.Error.WriteLine("MSBuild telemetry: " + message);
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // Diagnostics are best effort, for example the standard error stream may be closed.
            }
        }

        /// <summary>
        /// Describes an exception without its message, which may contain paths.
        /// </summary>
        internal static string DescribeException(Exception exception)
        {
            Exception baseException = exception.GetBaseException();
            return ReferenceEquals(baseException, exception)
                ? Describe(exception)
                : $"{Describe(exception)} caused by {Describe(baseException)}";

            static string Describe(Exception exception)
            {
                string description = $"{exception.GetType().FullName} (0x{exception.HResult:X8})";
                string? fileName = exception switch
                {
                    FileNotFoundException fileNotFound => fileNotFound.FileName,
                    FileLoadException fileLoad => fileLoad.FileName,
                    BadImageFormatException badImage => badImage.FileName,
                    _ => null,
                };

                return string.IsNullOrEmpty(fileName) ? description : $"{description} for assembly '{GetAssemblyName(fileName!)}'";
            }

            // Load failures report an assembly display name, or a path or URI when binding through a codeBase.
            static string GetAssemblyName(string fileName)
            {
                try
                {
                    return fileName.IndexOfAny(['\\', '/']) >= 0
                        ? Path.GetFileNameWithoutExtension(fileName)
                        : new AssemblyName(fileName).Name ?? "unknown";
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    return "unknown";
                }
            }
        }

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

            if (TelemetryManager.IsDiagnosticsEnabled())
            {
                string state = session is null
                    ? "no session is available"
                    : $"{(isStandalone ? "owned" : "host")} session, {(session.IsOptedIn ? "opted in" : "not opted in, events will not be sent")}";
                TelemetryManager.WriteDiagnostic(
                    $"initialized ({state}, CI: {BuildEnvironmentState.IsAutomatedEnvironment()}) using {DescribeAssembly(typeof(TelemetrySession).Assembly)}.");
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

            // Checked without telemetry types, so that processes without a session do not load the telemetry assembly.
            if (ownsSession && telemetrySession is not null)
            {
                DisposeOwnedSession(telemetrySession);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DisposeOwnedSession(object telemetrySession)
        {
            TelemetrySession session = (TelemetrySession)telemetrySession;

            // An ephemeral CI agent may be discarded before another process uploads the persisted events.
            bool upload = BuildEnvironmentState.IsAutomatedEnvironment();
            TimeSpan timeout = TelemetryManager.GetShutdownTimeout();
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Disposing can block on the network, so the exiting process abandons the thread after the budget.
            bool completed = Task.Run(() =>
            {
                if (upload)
                {
                    return session.DisposeToNetworkAsync(CancellationToken.None);
                }

                session.Dispose();
                return Task.CompletedTask;
            }).Wait(timeout);

            TelemetryManager.WriteDiagnostic(
                $"{(upload ? "upload" : "save")} {(completed ? "completed" : "timed out, pending events may be lost")} after {stopwatch.ElapsedMilliseconds} ms, budget {(long)timeout.TotalMilliseconds} ms.");
        }

        // Reports the location relative to MSBuild.exe, because the full path may contain a user name.
        private static string DescribeAssembly(Assembly assembly)
        {
            string version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "unknown";
            string location;
            string? assemblyDirectory = string.IsNullOrEmpty(assembly.Location) ? null : Path.GetDirectoryName(assembly.Location);
            string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            if (assembly.GlobalAssemblyCache)
            {
                location = "the global assembly cache";
            }
            else if (string.Equals(assemblyDirectory, applicationDirectory, StringComparison.OrdinalIgnoreCase))
            {
                location = "the application directory";
            }
            else if (string.Equals(assemblyDirectory, Path.GetDirectoryName(applicationDirectory), StringComparison.OrdinalIgnoreCase))
            {
                location = "the parent of the application directory";
            }
            else
            {
                location = "another directory";
            }

            return $"{assembly.GetName().Name} {version} from {location}";
        }
    }
#endif
}
