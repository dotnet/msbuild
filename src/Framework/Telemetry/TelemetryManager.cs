// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System.Threading.Tasks;
using Microsoft.VisualStudio.Telemetry;
#endif

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

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

        // Signaled when the shutdown started by the first Dispose() call has finished, so that a later call
        // can wait for it instead of returning while the session is still being saved or uploaded.
        private static readonly ManualResetEventSlim s_shutdownCompleted = new(initialState: false);
        private static Stopwatch? s_shutdownStopwatch;

        /// <summary>
        /// Indicates whether the telemetry infrastructure has been torn down, or its teardown has started.
        /// Exposed for TESTING purposes.
        /// </summary>
        internal static bool IsDisposed => s_disposed;

        /// <summary>
        /// Indicates that this process is MSBuild.exe itself, started from the command line (as opposed to a process that hosts MSBuild,
        /// such as Visual Studio or a test host), so no host owns a telemetry session for it and MSBuild may create its own.
        /// Only <c>MSBuildApp.Main</c> sets it. It decides whether a process that crashes without having started a session,
        /// such as a worker, task host or RAR node, creates a session just to report the crash. See <see cref="InitializeForCrash"/>.
        /// </summary>
        internal static bool IsStandaloneProcess { get; set; }

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
                    WriteDiagnostic("opted out.");
                    return;
                }

                TryInitializeTelemetry(isStandalone);
            }
        }

        /// <summary>
        /// Initializes telemetry on a path that reports a crash, in a process that may not have started a session up front:
        /// only the process that builds in-process (the entry process or the server node) does so, so a worker, task host or RAR node
        /// has none when it crashes, and nothing else can report the crash for it.
        /// In MSBuild.exe (<see cref="IsStandaloneProcess"/>) this creates a session just for the crash, which the shutdown at the end of
        /// <c>Main</c>, or <see cref="CrashTelemetryRecorder.RecordAndFlushCrashTelemetry"/>, saves or uploads within the shutdown budget.
        /// In a process that MSBuild is hosted in, it uses the host's session, and does nothing if there is none.
        /// Does nothing when a session was already started.
        /// </summary>
        public void InitializeForCrash() => Initialize(IsStandaloneProcess);

        /// <summary>
        /// Resets the TelemetryManager state for TESTING purposes.
        /// </summary>
        internal static void ResetForTest()
        {
            lock (s_lock)
            {
                s_initialized = false;
                s_disposed = false;
                s_shutdownStopwatch = null;
                s_shutdownCompleted.Reset();
                IsStandaloneProcess = false;
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
                WriteDiagnostic("initialization failed, telemetry is disabled for this process: " + ex);
            }
        }

        /// <summary>
        /// Shuts telemetry down, saving or uploading the pending events of a session that this process owns within the shutdown budget
        /// (<c>MSBUILD_TELEMETRY_SHUTDOWN_TIMEOUT_MS</c>). It can be called more than once, for example by <c>Main</c> and by the
        /// unhandled exception handler. A later call waits for the shutdown the first one started, within what is left of the budget,
        /// so that the process doesn't exit under it.
        /// </summary>
        public void Dispose() => Dispose(ShutdownSession, Traits.Instance.TelemetryShutdownTimeoutMs);

        /// <summary>
        /// Exposed for deterministic testing without creating a real telemetry session.
        /// </summary>
        /// <param name="shutdown">Saves or uploads the pending events. Only the first call to Dispose runs it.</param>
        /// <param name="budgetMs">The time, counted from the start of the shutdown, that a later call waits for it to finish.</param>
        internal void Dispose(Action shutdown, int budgetMs)
        {
            Stopwatch shutdownStopwatch;
            bool isFirstCall;

            lock (s_lock)
            {
                isFirstCall = !s_disposed;

                if (isFirstCall)
                {
                    s_disposed = true;
                    s_shutdownStopwatch = Stopwatch.StartNew();

                    // Nothing may use the activity source once the underlying session is gone.
                    DefaultActivitySource = null;
                }

                shutdownStopwatch = s_shutdownStopwatch!;
            }

            if (!isFirstCall)
            {
                s_shutdownCompleted.Wait((int)Math.Max(0, budgetMs - shutdownStopwatch.ElapsedMilliseconds));
                return;
            }

            try
            {
                shutdown();
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // Telemetry is best effort and must never fail a build.
                // The Visual Studio telemetry assembly may never have been loaded (FileNotFoundException,
                // FileLoadException, TypeLoadException), and disposing the session can itself throw when the
                // telemetry stack was not fully started - for example when telemetry is opted out machine wide.
                // Critical exceptions still propagate because the process is not safe to continue.
                WriteDiagnostic("shutdown failed: " + ex);
            }
            finally
            {
                s_shutdownCompleted.Set();
            }
        }

        private static void ShutdownSession()
        {
#if NETFRAMEWORK
            DisposeVsTelemetry();
#endif
        }

        /// <summary>
        /// Determines if the user has explicitly opted out of telemetry.
        /// </summary>
        /// <remarks>
        /// The .NET SDK opt-out (<c>DOTNET_CLI_TELEMETRY_OPTOUT</c>) applies in every process, including MSBuild running inside Visual Studio,
        /// because this one check gates both the session and the collection of build data in <c>BuildManager.BeginBuild</c>.
        /// A user who opted out of .NET telemetry stays opted out wherever MSBuild runs, without a per-host list of exceptions.
        /// </remarks>
        internal static bool IsOptOut() =>
#if NETFRAMEWORK
            Traits.Instance.FrameworkTelemetryOptOut || Traits.Instance.SdkTelemetryOptOut;
#else
            Traits.Instance.SdkTelemetryOptOut;
#endif

        /// <summary>
        /// Writes a message to the standard error stream when MSBUILD_TELEMETRY_DIAGNOSTICS is set.
        /// </summary>
        internal static void WriteDiagnostic(string message)
        {
            if (Traits.Instance.TelemetryDiagnostics)
            {
                try
                {
                    Console.Error.WriteLine("MSBuild telemetry: " + message);
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    // Console may not be available (e.g. redirected to a broken pipe).
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
        private static object? s_activitySource;
        private static bool s_ownsSession = false;

        // The upload gets four fifths of the shutdown budget. The rest is kept for saving the events locally when it does not complete.
        private const int SaveReserveDivisor = 5;

        internal enum SessionShutdownResult
        {
            /// <summary>The pending events were uploaded, or saved when no upload was requested, within the budget.</summary>
            Completed,

            /// <summary>The upload failed or ran out of time, and the pending events were saved so that a later process can upload them.</summary>
            SavedInsteadOfUploaded,

            /// <summary>Nothing completed within the budget, so the pending events may be lost.</summary>
            TimedOut,
        }

        private enum TaskOutcome
        {
            Succeeded,
            Failed,
            StillRunning,
        }

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

            if (Traits.Instance.TelemetryDiagnostics)
            {
                string state = session is null
                    ? "no session is available"
                    : $"{(isStandalone ? "owned" : "host")} session, {(session.IsOptedIn ? "opted in" : "not opted in, events will not be sent")}";
                TelemetryManager.WriteDiagnostic(
                    $"initialized ({state}, CI: {BuildEnvironmentState.IsAutomatedEnvironment()}) using {typeof(TelemetrySession).Assembly.Location}.");
            }

            MSBuildActivitySource activitySource = new(session);
            s_activitySource = activitySource;
            return activitySource;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Dispose()
        {
            object? telemetrySession = s_telemetrySession;
            object? activitySource = s_activitySource;
            bool ownsSession = s_ownsSession;

            // Clear the state first so that a failure while disposing the session cannot leave a stale
            // session behind, and so that a subsequent disposal attempt is a no-op.
            s_telemetrySession = null;
            s_activitySource = null;
            s_ownsSession = false;

            // Checked without telemetry types, so that processes without a session do not load the telemetry assembly.
            if (ownsSession && telemetrySession is not null)
            {
                DisposeOwnedSession(telemetrySession, activitySource);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DisposeOwnedSession(object telemetrySession, object? activitySource)
        {
            TelemetrySession session = (TelemetrySession)telemetrySession;

            bool isAutomatedEnvironment = BuildEnvironmentState.IsAutomatedEnvironment();
            bool hasStartedEvents = activitySource is MSBuildActivitySource { HasStartedActivities: true };
            bool isOptedIn = IsOptedIn(session);
            bool upload = ShouldUploadOnShutdown(isAutomatedEnvironment, hasStartedEvents, isOptedIn);
            int timeoutMs = Traits.Instance.TelemetryShutdownTimeoutMs;
            Stopwatch stopwatch = Stopwatch.StartNew();

            SessionShutdownResult result = DisposeOwnedSession(
                upload,
                timeoutMs,
                cancellationToken => session.DisposeToNetworkAsync(cancellationToken),
                session.Dispose);

            string outcome = result switch
            {
                SessionShutdownResult.Completed => "completed",
                SessionShutdownResult.SavedInsteadOfUploaded => "did not complete, the pending events were saved for a later upload",
                _ => "timed out, pending events may be lost",
            };

            TelemetryManager.WriteDiagnostic(
                $"{(upload ? "upload" : "save")} {outcome} after {stopwatch.ElapsedMilliseconds} ms, budget {timeoutMs} ms (CI: {isAutomatedEnvironment}, events started: {hasStartedEvents}, opted in: {isOptedIn}).");
        }

        private static bool IsOptedIn(TelemetrySession session)
        {
            try
            {
                return session.IsOptedIn;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // Saving the events locally is the safe choice when the consent cannot be read.
                TelemetryManager.WriteDiagnostic("reading the consent failed: " + ex);
                return false;
            }
        }

        /// <summary>
        /// Decides whether exiting should wait for the pending events to be uploaded, rather than only save them for a later upload.
        /// An ephemeral CI agent can be discarded before another process uploads what was saved, so there the upload is worth a bounded wait.
        /// The trigger is the shared automated-environment detection, which the terminal logger uses too: a false positive, such as a
        /// generic variable like <c>BUILD_ID</c> on a developer machine, costs at most the shutdown budget and then a local save,
        /// while a false negative loses the events of an agent that is discarded.
        /// Waiting is pointless when the session is not opted in, because nothing is sent, or when the process started no events.
        /// </summary>
        internal static bool ShouldUploadOnShutdown(bool isAutomatedEnvironment, bool hasStartedEvents, bool isOptedIn)
            => isAutomatedEnvironment && hasStartedEvents && isOptedIn;

        /// <summary>
        /// Saves or uploads the pending events of an owned telemetry session, taking <paramref name="timeoutMs"/> in total at most.
        /// Exposed for deterministic testing without creating a real Visual Studio telemetry session.
        /// </summary>
        /// <param name="upload">
        /// <c>true</c> to wait for the events to be uploaded. The upload gets most of the budget. If it fails or does not complete in time,
        /// it is canceled and, only once it has stopped, the session is disposed to save the events so that a later process can upload them.
        /// The two never run at the same time. <c>false</c> only saves the events.
        /// </param>
        /// <param name="timeoutMs">The total budget. Zero doesn't wait.</param>
        /// <param name="disposeToNetworkAsync">Uploads the pending events and disposes the session. It has to honor the cancellation token.</param>
        /// <param name="dispose">Saves the pending events and disposes the session.</param>
        internal static SessionShutdownResult DisposeOwnedSession(
            bool upload,
            int timeoutMs,
            Func<CancellationToken, Task> disposeToNetworkAsync,
            Action dispose)
        {
            if (!upload)
            {
                return RunWithinBudget(dispose, timeoutMs) ? SessionShutdownResult.Completed : SessionShutdownResult.TimedOut;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            // Deliberately not disposed: an upload that outlives the budget can still be using the token while the process exits,
            // and a source without timers or linked tokens holds nothing that outlives the process.
#pragma warning disable CA2000 // Dispose objects before losing scope
            CancellationTokenSource cancellation = new();
#pragma warning restore CA2000 // Dispose objects before losing scope

            // Disposing can block on the network, so the exiting process abandons the thread after the budget.
            Task uploadTask = Task.Run(() => disposeToNetworkAsync(cancellation.Token));

            TaskOutcome outcome = Wait(uploadTask, timeoutMs - (timeoutMs / SaveReserveDivisor), out Exception? failure);

            if (outcome == TaskOutcome.Failed)
            {
                TelemetryManager.WriteDiagnostic("upload failed: " + failure);
            }
            else if (outcome == TaskOutcome.StillRunning)
            {
                // The upload saves the pending events before it transmits them, and stops when canceled.
                cancellation.Cancel();
                outcome = Wait(uploadTask, RemainingMs(timeoutMs, stopwatch), out _);
            }

            switch (outcome)
            {
                case TaskOutcome.Succeeded:
                    return SessionShutdownResult.Completed;
                case TaskOutcome.StillRunning:
                    // Disposing the session now would race with the upload that is still using it.
                    return SessionShutdownResult.TimedOut;
                default:
                    return RunWithinBudget(dispose, RemainingMs(timeoutMs, stopwatch))
                        ? SessionShutdownResult.SavedInsteadOfUploaded
                        : SessionShutdownResult.TimedOut;
            }
        }

        private static bool RunWithinBudget(Action action, int timeoutMs) => Task.Run(action).Wait(timeoutMs);

        private static int RemainingMs(int timeoutMs, Stopwatch stopwatch) => (int)Math.Max(0, timeoutMs - stopwatch.ElapsedMilliseconds);

        private static TaskOutcome Wait(Task task, int timeoutMs, out Exception? failure)
        {
            failure = null;

            try
            {
                return task.Wait(timeoutMs) ? TaskOutcome.Succeeded : TaskOutcome.StillRunning;
            }
            catch (AggregateException ex)
            {
                // The task faulted or was canceled, which means it has stopped.
                failure = ex.GetBaseException();
                return TaskOutcome.Failed;
            }
        }
    }
#endif
}
