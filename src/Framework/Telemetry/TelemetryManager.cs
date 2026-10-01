// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using Microsoft.VisualStudio.Telemetry;
#endif

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Shuts down a telemetry session owned by this process: uploads its events when <paramref name="transmit"/> is true,
    /// otherwise persists them locally for a later process to upload.
    /// </summary>
    internal delegate Task OwnedTelemetrySessionShutdown(bool transmit, CancellationToken cancellationToken);

    internal enum TelemetryShutdownOutcome
    {
        NotOwned,
        Completed,
        TimedOut,
        Failed,
    }

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
        private static OwnedTelemetrySessionShutdown? s_ownedSessionShutdownForTest;

        /// <summary>
        /// Indicates whether the telemetry infrastructure has already been torn down.
        /// Exposed for TESTING purposes.
        /// </summary>
        internal static bool IsDisposed => s_disposed;

        // Exposed for TESTING purposes.
        internal static TelemetryShutdownOutcome LastShutdownOutcome { get; private set; }
        internal static TextWriter? DiagnosticsWriterForTest { get; set; }
        internal static Func<bool, MSBuildActivitySource?>? InitializerForTest { get; set; }

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
                s_ownedSessionShutdownForTest = null;
                InitializerForTest = null;
                LastShutdownOutcome = TelemetryShutdownOutcome.NotOwned;
                Instance.DefaultActivitySource = null;
            }
        }

        /// <summary>
        /// Registers a session that <see cref="Dispose"/> shuts down as owned, so tests never need a real telemetry transport.
        /// </summary>
        internal static void SetOwnedSessionShutdownForTest(OwnedTelemetrySessionShutdown? shutdown)
        {
            lock (s_lock)
            {
                s_ownedSessionShutdownForTest = shutdown;
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
                if (InitializerForTest is not null)
                {
                    DefaultActivitySource = InitializerForTest(isStandalone);
                    return;
                }

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

        /// <summary>
        /// Shuts down telemetry for the rest of the process. Only a session owned by this process is shut down: in CI its events
        /// are uploaded, because an ephemeral agent may never run another process that would upload them; elsewhere they are persisted.
        /// </summary>
        public void Dispose()
        {
            OwnedTelemetrySessionShutdown? shutdown;
            lock (s_lock)
            {
                if (s_disposed)
                {
                    return;
                }

                s_disposed = true;

                // Nothing may use the activity source once the underlying session is gone.
                DefaultActivitySource = null;

                shutdown = DetachOwnedSessionShutdown();
            }

            try
            {
                LastShutdownOutcome = shutdown is null
                    ? TelemetryShutdownOutcome.NotOwned
                    : ShutdownOwnedSession(shutdown, BuildEnvironmentState.IsAutomatedEnvironment(), GetShutdownTimeout());
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                LastShutdownOutcome = TelemetryShutdownOutcome.Failed;
                WriteDiagnostic($"shutdown failed: {DescribeException(ex)}.");
            }
        }

        internal static TelemetryShutdownOutcome ShutdownOwnedSession(OwnedTelemetrySessionShutdown shutdown, bool transmit, TimeSpan timeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
#pragma warning disable CA2000 // Dispose objects before losing scope - disposed by the continuation below.
            CancellationTokenSource cancellation = new();
#pragma warning restore CA2000
            CancellationToken cancellationToken = cancellation.Token;

            // A dedicated background thread starts even when the thread pool is saturated and cannot keep the process alive.
            Task task = Task.Factory.StartNew(
                () => shutdown(transmit, cancellationToken),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();

            cancellation.CancelAfter(timeout);

            TelemetryShutdownOutcome outcome;
            Exception? failure = null;
            try
            {
                outcome = task.Wait(timeout) ? TelemetryShutdownOutcome.Completed : TelemetryShutdownOutcome.TimedOut;
            }
            catch (AggregateException ex)
            {
                failure = ex.Flatten().InnerExceptions.FirstOrDefault(e => e is not OperationCanceledException);
                outcome = failure is null ? TelemetryShutdownOutcome.TimedOut : TelemetryShutdownOutcome.Failed;
            }

            task.ContinueWith(
                static (completedTask, state) =>
                {
                    _ = completedTask.Exception;
                    ((CancellationTokenSource)state!).Dispose();
                },
                cancellation,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            string result = outcome switch
            {
                TelemetryShutdownOutcome.Completed => "completed",
                TelemetryShutdownOutcome.TimedOut => "timed out, pending events may be lost",
                _ => $"failed: {DescribeException(failure!)}",
            };
            WriteDiagnostic($"shutdown ({(transmit ? "transmit to network (CI)" : "persist locally")}) {result} after {stopwatch.ElapsedMilliseconds} ms, budget {(long)timeout.TotalMilliseconds} ms.");

            return outcome;
        }

        internal static TimeSpan GetShutdownTimeout()
        {
            string? value = Environment.GetEnvironmentVariable(ShutdownTimeoutEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultShutdownTimeout;
            }

            if (int.TryParse(value!.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int milliseconds))
            {
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            WriteDiagnostic($"ignoring {ShutdownTimeoutEnvironmentVariable} because it is not a non-negative whole number of milliseconds; using {(long)DefaultShutdownTimeout.TotalMilliseconds} ms.");
            return DefaultShutdownTimeout;
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
                    (DiagnosticsWriterForTest ?? Console.Error).WriteLine("MSBuild telemetry: " + message);
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

        private static OwnedTelemetrySessionShutdown? DetachOwnedSessionShutdown()
        {
            OwnedTelemetrySessionShutdown? shutdown = null;
#if NETFRAMEWORK
            try
            {
                shutdown = DetachVsTelemetrySession();
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // Telemetry is best effort and must never fail a build.
                // The Visual Studio telemetry assembly may not be loadable (FileNotFoundException, FileLoadException, TypeLoadException).
                WriteDiagnostic($"could not shut down the telemetry session: {DescribeException(ex)}.");
            }
#endif
            shutdown = s_ownedSessionShutdownForTest ?? shutdown;
            s_ownedSessionShutdownForTest = null;
            return shutdown;
        }

#if NETFRAMEWORK
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static OwnedTelemetrySessionShutdown? DetachVsTelemetrySession() => VsTelemetryInitializer.DetachOwnedSession();
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

        /// <summary>
        /// Clears the session state and returns the shutdown of the session if this process owns it. Host sessions are never shut down.
        /// Must not reference telemetry types, so that processes without a session do not load the telemetry assembly on shutdown.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static OwnedTelemetrySessionShutdown? DetachOwnedSession()
        {
            object? telemetrySession = s_telemetrySession;
            bool ownsSession = s_ownsSession;

            // Clear the state first so that a failure while disposing the session cannot leave a stale
            // session behind, and so that a subsequent disposal attempt is a no-op.
            s_telemetrySession = null;
            s_ownsSession = false;

            return ownsSession && telemetrySession is not null ? CreateShutdown(telemetrySession) : null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static OwnedTelemetrySessionShutdown? CreateShutdown(object telemetrySession)
        {
            if (telemetrySession is not TelemetrySession session)
            {
                return null;
            }

            // DisposeToNetworkAsync uploads pending and previously persisted events; cancellation leaves unsent events in the local store.
            return (transmit, cancellationToken) =>
            {
                if (transmit)
                {
                    return session.DisposeToNetworkAsync(cancellationToken);
                }

                session.Dispose();
                return Task.CompletedTask;
            };
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
