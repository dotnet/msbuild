// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using Microsoft.VisualStudio.Telemetry;
#endif

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Completes a telemetry session that is owned by this process.
    /// </summary>
    /// <param name="transmit">
    /// When <c>true</c>, pending events should be uploaded over the network before the returned task completes.
    /// When <c>false</c>, pending events are only persisted locally, to be uploaded later by another process.
    /// </param>
    /// <param name="cancellationToken">Signaled when the shutdown budget is exhausted.</param>
    internal delegate Task OwnedTelemetrySessionShutdown(bool transmit, CancellationToken cancellationToken);

    /// <summary>
    /// Result of <see cref="TelemetryManager.Dispose"/>.
    /// </summary>
    internal enum TelemetryShutdownOutcome
    {
        /// <summary>The process did not own a telemetry session, so nothing was shut down.</summary>
        NotOwned,

        /// <summary>The owned session completed its shutdown within the budget.</summary>
        Completed,

        /// <summary>The shutdown budget was exhausted; the shutdown was cancelled and abandoned.</summary>
        TimedOut,

        /// <summary>The owned session failed to shut down. The failure was swallowed.</summary>
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
    /// The lifetime of a telemetry session created by <see cref="Initialize"/> in standalone mode is the lifetime of the process:
    /// it is shut down once, by <see cref="Dispose"/>, never per build request. Sessions borrowed from a host are never shut down.
    /// </remarks>
    internal class TelemetryManager
    {
        /// <summary>
        /// Environment variable that overrides the total time, in milliseconds, that <see cref="Dispose"/> waits
        /// for an owned telemetry session to persist or transmit its events. <c>0</c> means do not wait.
        /// </summary>
        internal const string ShutdownTimeoutEnvironmentVariable = "MSBUILD_TELEMETRY_SHUTDOWN_TIMEOUT_MS";

        /// <summary>
        /// Environment variable that, when <c>1</c> or <c>true</c>, writes telemetry status messages to the standard error stream.
        /// </summary>
        internal const string DiagnosticsEnvironmentVariable = "MSBUILD_TELEMETRY_DIAGNOSTICS";

        private const string DiagnosticsPrefix = "MSBuild telemetry: ";

        internal const string OptOutEnvironmentVariable =
#if NETFRAMEWORK
            "MSBUILD_TELEMETRY_OPTOUT";
#else
            "DOTNET_CLI_TELEMETRY_OPTOUT";
#endif

        /// <summary>
        /// Default total shutdown budget. There is a single owned session, so this is the budget for the whole shutdown.
        /// It matches the Visual Studio telemetry stack's own maximum wait for its manifest on dispose, and is half of the
        /// .NET SDK's per-provider CI shutdown timeout.
        /// </summary>
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

        /// <summary>
        /// The outcome of the last <see cref="Dispose"/> that shut down telemetry.
        /// Exposed for TESTING purposes.
        /// </summary>
        internal static TelemetryShutdownOutcome LastShutdownOutcome { get; private set; }

        /// <summary>
        /// Replaces the standard error stream as the destination of diagnostics messages.
        /// Exposed for TESTING purposes.
        /// </summary>
        internal static TextWriter? DiagnosticsWriterForTest { get; set; }

        /// <summary>
        /// Replaces the platform telemetry initialization, for example to simulate a missing telemetry assembly.
        /// Exposed for TESTING purposes.
        /// </summary>
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
                // Telemetry is shut down for the rest of the process once disposed; a session created now would never be shut down.
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
        /// Registers a session that <see cref="Dispose"/> treats as owned by this process.
        /// Exposed for TESTING purposes, so tests never need a real telemetry transport.
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
        /// Shuts down telemetry for the rest of the process lifetime. Call once, when the process is about to exit.
        /// </summary>
        /// <remarks>
        /// Only a session owned by this process is shut down. On a CI agent (see <see cref="CIEnvironmentDetector"/>) the session
        /// tries to upload its events before the process exits, because an ephemeral agent may never run another process that would
        /// upload persisted events. Elsewhere the session persists events locally, as before. Either way the wait is bounded by
        /// <see cref="GetShutdownTimeout"/>, happens outside of any lock, and never throws for non-critical failures.
        /// </remarks>
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

            LastShutdownOutcome = shutdown is null
                ? TelemetryShutdownOutcome.NotOwned
                : ShutdownOwnedSession(shutdown, CIEnvironmentDetector.IsCIEnvironment(), GetShutdownTimeout());
        }

        /// <summary>
        /// Runs <paramref name="shutdown"/> and waits at most <paramref name="timeout"/> for it to complete.
        /// </summary>
        /// <remarks>
        /// The shutdown runs on a dedicated background thread, so it starts immediately even when the thread pool is saturated and
        /// an abandoned shutdown cannot keep the process alive. When the budget is exhausted, the shutdown's cancellation token is
        /// signaled so the transport stops sending, and the caller returns without waiting further.
        /// </remarks>
        internal static TelemetryShutdownOutcome ShutdownOwnedSession(OwnedTelemetrySessionShutdown shutdown, bool transmit, TimeSpan timeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
#pragma warning disable CA2000 // Dispose objects before losing scope - disposed below, or by the continuation once an abandoned shutdown completes.
            CancellationTokenSource cancellation = new();
#pragma warning restore CA2000
            CancellationToken cancellationToken = cancellation.Token;
            TelemetryShutdownOutcome outcome;
            Exception? failure = null;
            Task? task = null;

            try
            {
                task = Task.Factory.StartNew(
                    () => shutdown(transmit, cancellationToken) ?? Task.CompletedTask,
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap();

                cancellation.CancelAfter(timeout);
                outcome = task.Wait(timeout) ? TelemetryShutdownOutcome.Completed : TelemetryShutdownOutcome.TimedOut;
            }
            catch (AggregateException ex)
            {
                failure = FindNonCancellationException(ex);
                outcome = failure is null ? TelemetryShutdownOutcome.TimedOut : TelemetryShutdownOutcome.Failed;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                failure = ex;
                outcome = TelemetryShutdownOutcome.Failed;
            }

            stopwatch.Stop();

            if (task is null)
            {
                cancellation.Dispose();
            }
            else
            {
                // Observe any later failure of an abandoned shutdown and release the token source only once nothing uses it.
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
            }

            if (IsDiagnosticsEnabled())
            {
                string mode = transmit ? "transmit to network (CI)" : "persist locally";
                string result = outcome switch
                {
                    TelemetryShutdownOutcome.Completed => "completed",
                    TelemetryShutdownOutcome.TimedOut => "timed out, pending events may be lost",
                    _ => $"failed: {DescribeException(failure!)}",
                };
                WriteDiagnostic(string.Format(
                    CultureInfo.InvariantCulture,
                    "shutdown ({0}) {1} after {2} ms, budget {3} ms.",
                    mode,
                    result,
                    stopwatch.ElapsedMilliseconds,
                    (long)timeout.TotalMilliseconds));
            }

            return outcome;
        }

        /// <summary>
        /// Gets the total time <see cref="Dispose"/> may wait for the owned session, from <see cref="ShutdownTimeoutEnvironmentVariable"/>
        /// or <see cref="DefaultShutdownTimeout"/>.
        /// </summary>
        internal static TimeSpan GetShutdownTimeout()
        {
            string? value;
            try
            {
                value = Environment.GetEnvironmentVariable(ShutdownTimeoutEnvironmentVariable);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                return DefaultShutdownTimeout;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultShutdownTimeout;
            }

            if (int.TryParse(value!.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int milliseconds))
            {
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            WriteDiagnostic(string.Format(
                CultureInfo.InvariantCulture,
                "ignoring {0} because it is not a non-negative whole number of milliseconds; using {1} ms.",
                ShutdownTimeoutEnvironmentVariable,
                (long)DefaultShutdownTimeout.TotalMilliseconds));

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

        internal static bool IsDiagnosticsEnabled()
        {
            try
            {
                return EnvironmentUtilities.IsValueOneOrTrue(DiagnosticsEnvironmentVariable);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                return false;
            }
        }

        /// <summary>
        /// Writes a telemetry status message to the standard error stream when <see cref="DiagnosticsEnvironmentVariable"/> is set.
        /// Messages must not contain paths, user data, or event contents.
        /// </summary>
        internal static void WriteDiagnostic(string message)
        {
            if (!IsDiagnosticsEnabled())
            {
                return;
            }

            try
            {
                (DiagnosticsWriterForTest ?? Console.Error).WriteLine(DiagnosticsPrefix + message);
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // Diagnostics are best effort, for example the standard error stream may be closed.
            }
        }

        /// <summary>
        /// Describes an exception without its message, which may contain paths: its type, HRESULT and, for assembly load failures,
        /// the simple name of the assembly.
        /// </summary>
        internal static string DescribeException(Exception exception)
        {
            string description = DescribeSingleException(exception);
            Exception baseException = exception.GetBaseException();
            return ReferenceEquals(baseException, exception)
                ? description
                : $"{description} caused by {DescribeSingleException(baseException)}";
        }

        private static string DescribeSingleException(Exception exception)
        {
            string description = string.Format(CultureInfo.InvariantCulture, "{0} (0x{1:X8})", exception.GetType().FullName, exception.HResult);
            string? fileName = exception switch
            {
                FileNotFoundException fileNotFound => fileNotFound.FileName,
                FileLoadException fileLoad => fileLoad.FileName,
                BadImageFormatException badImage => badImage.FileName,
                _ => null,
            };

            return string.IsNullOrEmpty(fileName) ? description : $"{description} for assembly '{GetSimpleAssemblyName(fileName!)}'";
        }

        private static string GetSimpleAssemblyName(string fileName)
        {
            try
            {
                // Load failures report either an assembly display name or a path/URI (for example from a codeBase). Never report directories.
                if (fileName.IndexOfAny(['\\', '/']) >= 0)
                {
                    return Path.GetFileNameWithoutExtension(fileName);
                }

                return new AssemblyName(fileName).Name ?? "unknown";
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                return "unknown";
            }
        }

        private static Exception? FindNonCancellationException(AggregateException exception)
        {
            foreach (Exception innerException in exception.Flatten().InnerExceptions)
            {
                if (innerException is not OperationCanceledException)
                {
                    return innerException;
                }
            }

            return null;
        }

#if NETFRAMEWORK
        /// <summary>
        /// Describes where an assembly was loaded from without revealing its path.
        /// </summary>
        internal static string DescribeAssembly(Assembly assembly)
        {
            string name = assembly.GetName().Name ?? "unknown";
            string version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "unknown";
            return $"{name} {version} from {DescribeAssemblyLocation(assembly)}";
        }

        private static string DescribeAssemblyLocation(Assembly assembly)
        {
            try
            {
                if (assembly.GlobalAssemblyCache)
                {
                    return "the global assembly cache";
                }

                string? assemblyDirectory = Path.GetDirectoryName(assembly.Location);
                string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(assemblyDirectory, applicationDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return "the application directory";
                }

                if (string.Equals(assemblyDirectory, Path.GetDirectoryName(applicationDirectory), StringComparison.OrdinalIgnoreCase))
                {
                    return "the parent of the application directory";
                }

                return "another directory";
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                return "an unknown location";
            }
        }
#endif

        private static OwnedTelemetrySessionShutdown? DetachOwnedSessionShutdown()
        {
            OwnedTelemetrySessionShutdown? shutdown = s_ownedSessionShutdownForTest;
            s_ownedSessionShutdownForTest = null;

#if NETFRAMEWORK
            try
            {
                // Always detach, so that the session state is cleared even if a test registered its own session.
                OwnedTelemetrySessionShutdown? vsShutdown = DetachVsTelemetrySession();
                shutdown ??= vsShutdown;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // Telemetry is best effort and must never fail a build.
                // The Visual Studio telemetry assembly may not be loadable (FileNotFoundException, FileLoadException, TypeLoadException).
                WriteDiagnostic($"could not shut down the telemetry session: {DescribeException(ex)}.");
            }
#endif

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
                WriteInitializationDiagnostic(session, isStandalone);
            }

            return new MSBuildActivitySource(session);
        }

        /// <summary>
        /// Clears the session state and, if this process owns the session, returns the operation that shuts it down.
        /// Sessions borrowed from a host, for example Visual Studio, belong to that host and are never shut down here.
        /// </summary>
        /// <remarks>
        /// This method does not reference Visual Studio telemetry types, so processes that never created a session
        /// do not load the telemetry assembly on shutdown.
        /// </remarks>
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

            return (transmit, cancellationToken) =>
            {
                if (transmit)
                {
                    // Flushes pending events to the local store, then uploads all stored events. Cancellation stops the upload;
                    // events that were not sent stay in the local store. Uploading requires this process to hold the store's
                    // cross-process mutex; otherwise the process that holds it uploads them.
                    return session.DisposeToNetworkAsync(cancellationToken);
                }

                // Persists pending events to the local store; a later telemetry process uploads them.
                session.Dispose();
                return Task.CompletedTask;
            };
        }

        private static void WriteInitializationDiagnostic(TelemetrySession? session, bool isStandalone)
        {
            string sessionDescription;
            if (session is null)
            {
                sessionDescription = "no session is available";
            }
            else
            {
                string optedIn;
                try
                {
                    optedIn = session.IsOptedIn ? "opted in" : "not opted in, events will not be sent";
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    optedIn = "consent unknown";
                }

                sessionDescription = $"{(isStandalone ? "owned" : "host")} session, {optedIn}";
            }

            TelemetryManager.WriteDiagnostic(
                $"initialized ({sessionDescription}, CI: {CIEnvironmentDetector.IsCIEnvironment()}) using {TelemetryManager.DescribeAssembly(typeof(TelemetrySession).Assembly)}.");
        }
    }
#endif
}
