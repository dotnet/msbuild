// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
#if NETFRAMEWORK
using System.Reflection;
using System.Runtime.Serialization;
using SessionShutdownResult = Microsoft.Build.Framework.Telemetry.VsTelemetryInitializer.SessionShutdownResult;
#else
using System.Collections.Generic;
#endif

using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

/// <summary>
/// Telemetry is best effort infrastructure - it must never be able to fail a build.
/// </summary>
/// <remarks>
/// The telemetry state is process wide, so every test that uses it belongs in this class, whose tests xUnit runs one at a time.
/// </remarks>
public class TelemetryManager_Tests
{
    [Fact]
    public void DisposeWithoutInitializeDoesNotThrow()
    {
        TelemetryManager.ResetForTest();

        // On .NET Framework this reaches into the Visual Studio telemetry stack, which may not even be
        // loadable in the test host. Whatever it throws must not escape.
        Should.NotThrow(() => TelemetryManager.Instance.Dispose());

        TelemetryManager.IsDisposed.ShouldBeTrue();

        TelemetryManager.ResetForTest();
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        TelemetryManager.ResetForTest();

        TelemetryManager.Instance.Dispose();
        Should.NotThrow(() => TelemetryManager.Instance.Dispose());

        TelemetryManager.IsDisposed.ShouldBeTrue();

        TelemetryManager.ResetForTest();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    public void DotnetCliTelemetryOptOutDisablesTelemetry(string value)
    {
        TestEnvironment environment = TestEnvironment.Create();

        try
        {
            environment.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", null);
            environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", value);
            Traits.UpdateFromEnvironment();

            TelemetryManager.IsOptOut().ShouldBeTrue();
        }
        finally
        {
            environment.Dispose();
            Traits.UpdateFromEnvironment();
        }
    }

#if NETFRAMEWORK
    [Fact]
    public void OwnedSessionInCiUsesNetworkDisposal()
    {
        bool uploadCalled = false;
        bool disposeCalled = false;

        SessionShutdownResult result = VsTelemetryInitializer.DisposeOwnedSession(
            upload: true,
            timeoutMs: 1_000,
            disposeToNetworkAsync: _ =>
            {
                uploadCalled = true;
                return Task.CompletedTask;
            },
            dispose: () => disposeCalled = true);

        result.ShouldBe(SessionShutdownResult.Completed);
        uploadCalled.ShouldBeTrue();
        disposeCalled.ShouldBeFalse();
    }

    [Fact]
    public void OwnedSessionOutsideCiUsesLocalDisposal()
    {
        bool uploadCalled = false;
        bool disposeCalled = false;

        SessionShutdownResult result = VsTelemetryInitializer.DisposeOwnedSession(
            upload: false,
            timeoutMs: 1_000,
            disposeToNetworkAsync: _ =>
            {
                uploadCalled = true;
                return Task.CompletedTask;
            },
            dispose: () => disposeCalled = true);

        result.ShouldBe(SessionShutdownResult.Completed);
        uploadCalled.ShouldBeFalse();
        disposeCalled.ShouldBeTrue();
    }

    [Fact]
    public void OwnedSessionShutdownHonorsConfiguredTimeout()
    {
        const int timeoutMs = 50;
        TaskCompletionSource<object?> uploadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken uploadToken = default;
        bool disposeCalled = false;
        Stopwatch stopwatch = Stopwatch.StartNew();

        SessionShutdownResult result;
        try
        {
            // An upload that ignores the cancellation, as one that is stuck in the network stack might.
            result = VsTelemetryInitializer.DisposeOwnedSession(
                upload: true,
                timeoutMs,
                cancellationToken =>
                {
                    uploadToken = cancellationToken;
                    return uploadCompletion.Task;
                },
                () => disposeCalled = true);
        }
        finally
        {
            uploadCompletion.TrySetResult(null);
        }

        stopwatch.Stop();
        result.ShouldBe(SessionShutdownResult.TimedOut);
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(timeoutMs / 2);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2_000);

        // The upload is asked to stop, but the events are not saved while it may still be using the session.
        uploadToken.IsCancellationRequested.ShouldBeTrue();
        disposeCalled.ShouldBeFalse();
    }

    [Fact]
    public void OwnedSessionEventsAreSavedAfterAnUploadThatDoesNotCompleteInTime()
    {
        bool uploadStopped = false;
        bool uploadStoppedBeforeSave = false;

        SessionShutdownResult result = VsTelemetryInitializer.DisposeOwnedSession(
            upload: true,
            timeoutMs: 1_000,
            cancellationToken =>
            {
                // An upload that honors the cancellation, as the telemetry library's does.
                TaskCompletionSource<object?> upload = new(TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() =>
                {
                    uploadStopped = true;
                    upload.TrySetCanceled(cancellationToken);
                });
                return upload.Task;
            },
            () => uploadStoppedBeforeSave = uploadStopped);

        result.ShouldBe(SessionShutdownResult.SavedInsteadOfUploaded);
        uploadStoppedBeforeSave.ShouldBeTrue("The events are saved only once the upload no longer uses the session.");
    }

    [Fact]
    public void OwnedSessionEventsAreSavedAfterAFailedUpload()
    {
        bool disposeCalled = false;
        Stopwatch stopwatch = Stopwatch.StartNew();

        SessionShutdownResult result = VsTelemetryInitializer.DisposeOwnedSession(
            upload: true,
            timeoutMs: 60_000,
            _ => Task.FromException(new InvalidOperationException("The network is not available.")),
            () => disposeCalled = true);

        // A failed upload doesn't wait for the rest of its share of the budget before the events are saved.
        result.ShouldBe(SessionShutdownResult.SavedInsteadOfUploaded);
        disposeCalled.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(30_000);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedSessionSaveThatDoesNotCompleteInTimeTimesOut(bool upload)
    {
        using ManualResetEventSlim releaseSave = new();

        try
        {
            SessionShutdownResult result = VsTelemetryInitializer.DisposeOwnedSession(
                upload,
                timeoutMs: 50,
                _ => Task.FromException(new InvalidOperationException("The network is not available.")),
                () => releaseSave.Wait());

            result.ShouldBe(SessionShutdownResult.TimedOut);
        }
        finally
        {
            releaseSave.Set();
        }
    }

    [Fact]
    public void OwnedSessionShutdownWithoutABudgetDoesNotWait()
    {
        using ManualResetEventSlim releaseUpload = new();
        bool disposeCalled = false;
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            SessionShutdownResult result = VsTelemetryInitializer.DisposeOwnedSession(
                upload: true,
                timeoutMs: 0,
                // An upload that ignores the cancellation.
                _ => Task.Run(() => releaseUpload.Wait(), CancellationToken.None),
                () => disposeCalled = true);

            result.ShouldBe(SessionShutdownResult.TimedOut);
            disposeCalled.ShouldBeFalse();
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2_000);
        }
        finally
        {
            releaseUpload.Set();
        }
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    public void UploadOnShutdownRequiresAnAutomatedEnvironmentStartedEventsAndConsent(
        bool isAutomatedEnvironment,
        bool hasStartedEvents,
        bool isOptedIn,
        bool expected)
        => VsTelemetryInitializer.ShouldUploadOnShutdown(isAutomatedEnvironment, hasStartedEvents, isOptedIn).ShouldBe(expected);

    [Fact]
    public void DisposeSwallowsTelemetrySessionNullReferenceAndClearsState()
    {
        TelemetryManager.ResetForTest();

        Type initializerType = typeof(TelemetryManager).Assembly.GetType(
            "Microsoft.Build.Framework.Telemetry.VsTelemetryInitializer")
            ?? throw new InvalidOperationException("VsTelemetryInitializer was not found.");
        FieldInfo sessionField = initializerType.GetField(
            "s_telemetrySession",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Telemetry session field was not found.");
        FieldInfo ownershipField = initializerType.GetField(
            "s_ownsSession",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Telemetry ownership field was not found.");

        Type telemetrySessionType = Assembly.Load("Microsoft.VisualStudio.Telemetry").GetType(
            "Microsoft.VisualStudio.Telemetry.TelemetrySession")
            ?? throw new InvalidOperationException("TelemetrySession was not found.");
        MethodInfo disposeMethod = telemetrySessionType.GetMethod(nameof(IDisposable.Dispose), Type.EmptyTypes)
            ?? throw new InvalidOperationException("TelemetrySession.Dispose was not found.");

        object controlSession = FormatterServices.GetUninitializedObject(telemetrySessionType);
        TargetInvocationException controlException = Should.Throw<TargetInvocationException>(
            () => disposeMethod.Invoke(controlSession, null));
        controlException.InnerException.ShouldBeOfType<NullReferenceException>();

        sessionField.SetValue(null, FormatterServices.GetUninitializedObject(telemetrySessionType));
        ownershipField.SetValue(null, true);

        try
        {
            Should.NotThrow(() => TelemetryManager.Instance.Dispose());

            TelemetryManager.IsDisposed.ShouldBeTrue();
            sessionField.GetValue(null).ShouldBeNull();
            ownershipField.GetValue(null).ShouldBe(false);
        }
        finally
        {
            sessionField.SetValue(null, null);
            ownershipField.SetValue(null, false);
            TelemetryManager.ResetForTest();
        }
    }
#endif

    [Fact]
    public void ShutdownRunsOnlyOnceWhenDisposedRepeatedly()
    {
        using TelemetryStateScope scope = new(optedOut: true, isStandaloneProcess: false);
        int shutdowns = 0;

        TelemetryManager.Instance.Dispose(() => shutdowns++, budgetMs: 1_000);
        TelemetryManager.Instance.Dispose(() => shutdowns++, budgetMs: 1_000);

        shutdowns.ShouldBe(1);
        TelemetryManager.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void LaterDisposeWaitsForTheShutdownInProgress()
    {
        using TelemetryStateScope scope = new(optedOut: true, isStandaloneProcess: false);
        using ManualResetEventSlim shutdownStarted = new();
        using ManualResetEventSlim releaseShutdown = new();
        bool shutdownFinished = false;
        bool shutdownFinishedWhenLaterDisposeReturned = false;

        Task firstDispose = Task.Run(() => TelemetryManager.Instance.Dispose(
            () =>
            {
                shutdownStarted.Set();
                releaseShutdown.Wait();
                shutdownFinished = true;
            },
            budgetMs: 60_000));

        try
        {
            shutdownStarted.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();

            // For example the unhandled exception handler racing with Main: returning early would let the process exit under the shutdown.
            Task laterDispose = Task.Run(() =>
            {
                TelemetryManager.Instance.Dispose(
                    () => throw new InvalidOperationException("Only the first call shuts down."),
                    budgetMs: 60_000);
                shutdownFinishedWhenLaterDisposeReturned = shutdownFinished;
            });

            laterDispose.Wait(TimeSpan.FromMilliseconds(250)).ShouldBeFalse("A later call has to wait for the shutdown that is in progress.");

            releaseShutdown.Set();

            laterDispose.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
            shutdownFinishedWhenLaterDisposeReturned.ShouldBeTrue();
        }
        finally
        {
            releaseShutdown.Set();
            firstDispose.Wait(TimeSpan.FromSeconds(30));
        }
    }

    [Fact]
    public void LaterDisposeStopsWaitingOnceTheShutdownBudgetIsSpent()
    {
        using TelemetryStateScope scope = new(optedOut: true, isStandaloneProcess: false);
        using ManualResetEventSlim shutdownStarted = new();
        using ManualResetEventSlim releaseShutdown = new();

        Task firstDispose = Task.Run(() => TelemetryManager.Instance.Dispose(
            () =>
            {
                shutdownStarted.Set();
                releaseShutdown.Wait();
            },
            budgetMs: 100));

        try
        {
            shutdownStarted.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();

            Task laterDispose = Task.Run(() => TelemetryManager.Instance.Dispose(() => { }, budgetMs: 100));

            // The shutdown is still blocked, so only the budget can end the wait.
            laterDispose.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue("A later call must give up once the budget is spent.");
            firstDispose.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            releaseShutdown.Set();
            firstDispose.Wait(TimeSpan.FromSeconds(30));
        }
    }

    [Fact]
    public void FailingShutdownDoesNotThrowAndDoesNotBlockLaterDisposes()
    {
        using TelemetryStateScope scope = new(optedOut: true, isStandaloneProcess: false);

        Should.NotThrow(() => TelemetryManager.Instance.Dispose(
            () => throw new InvalidOperationException("The telemetry stack is broken."),
            budgetMs: 60_000));

        TelemetryManager.IsDisposed.ShouldBeTrue();

        // The failed shutdown is over, so there is nothing to wait for. Waiting would take the whole budget.
        Stopwatch stopwatch = Stopwatch.StartNew();
        TelemetryManager.Instance.Dispose(() => { }, budgetMs: 60_000);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(30_000);
    }

    [Fact]
    public void CrashDoesNotStartASessionWhenTelemetryIsOptedOut()
    {
        using TelemetryStateScope scope = new(optedOut: true, isStandaloneProcess: true);
        RecordCrash();

        CrashTelemetryRecorder.FlushCrashTelemetry();

        KnownTelemetry.CrashTelemetry.ShouldBeNull();
        TelemetryManager.Instance.DefaultActivitySource.ShouldBeNull();
    }

    [Fact]
    public void RecordAndFlushCrashTelemetryShutsTelemetryDownWhenOptedOut()
    {
        using TelemetryStateScope scope = new(optedOut: true, isStandaloneProcess: true);

        Should.NotThrow(() => CrashTelemetryRecorder.RecordAndFlushCrashTelemetry(
            new InvalidOperationException("The build crashed."),
            CrashExitType.UnhandledException,
            isUnhandled: true,
            isCritical: false));

        TelemetryManager.IsDisposed.ShouldBeTrue();
        TelemetryManager.Instance.DefaultActivitySource.ShouldBeNull();
    }

#if NET
    // On .NET the telemetry is an ActivitySource, so these tests observe it with a listener and nothing is sent anywhere.
    [Fact]
    public void CrashInMSBuildExeStartsASessionToReportIt()
    {
        using TelemetryStateScope scope = new(optedOut: false, isStandaloneProcess: true);
        using ActivityCapture capture = new();
        RecordCrash();

        CrashTelemetryRecorder.FlushCrashTelemetry();

        // A worker, task host or RAR node has no session when it crashes, so one is created for the crash.
        KnownTelemetry.CrashTelemetry.ShouldBeNull();
        TelemetryManager.Instance.DefaultActivitySource.ShouldNotBeNull();
        Activity crash = capture.Started.ShouldHaveSingleItem();
        crash.DisplayName.ShouldBe($"{TelemetryConstants.EventPrefix}{TelemetryConstants.Crash}");
        crash.GetTagItem($"{TelemetryConstants.PropertyPrefix}{nameof(CrashTelemetry.ExceptionType)}").ShouldBe("System.InvalidOperationException");
    }

    [Fact]
    public void CrashInAHostedProcessDoesNotStartASession()
    {
        using TelemetryStateScope scope = new(optedOut: false, isStandaloneProcess: false);
        using ActivityCapture capture = new();
        RecordCrash();

        CrashTelemetryRecorder.FlushCrashTelemetry();

        // Only the host owns the session of a process that MSBuild is hosted in. Tests that call MSBuildApp.Execute in-process are hosted too.
        KnownTelemetry.CrashTelemetry.ShouldBeNull();
        TelemetryManager.Instance.DefaultActivitySource.ShouldBeNull();
        capture.Started.ShouldBeEmpty();
    }

    [Fact]
    public void CrashInAHostedProcessIsReportedThroughTheSessionOfTheHost()
    {
        using TelemetryStateScope scope = new(optedOut: false, isStandaloneProcess: false);
        using ActivityCapture capture = new();
        TelemetryManager.Instance.Initialize(isStandalone: false);
        RecordCrash();

        CrashTelemetryRecorder.FlushCrashTelemetry();

        capture.Started.ShouldHaveSingleItem().DisplayName.ShouldBe($"{TelemetryConstants.EventPrefix}{TelemetryConstants.Crash}");
    }

    [Fact]
    public void RecordAndFlushCrashTelemetryReportsTheCrashAndShutsTelemetryDown()
    {
        using TelemetryStateScope scope = new(optedOut: false, isStandaloneProcess: true);
        using ActivityCapture capture = new();

        CrashTelemetryRecorder.RecordAndFlushCrashTelemetry(
            new InvalidOperationException("The build crashed."),
            CrashExitType.UnhandledException,
            isUnhandled: true,
            isCritical: false);

        capture.Started.ShouldHaveSingleItem().DisplayName.ShouldBe($"{TelemetryConstants.EventPrefix}{TelemetryConstants.Crash}");
        TelemetryManager.IsDisposed.ShouldBeTrue();
        TelemetryManager.Instance.DefaultActivitySource.ShouldBeNull();
    }

    [Fact]
    public void ActivitySourceIsUnavailableOnceTheShutdownStarts()
    {
        using TelemetryStateScope scope = new(optedOut: false, isStandaloneProcess: false);
        TelemetryManager.Instance.Initialize(isStandalone: false);
        TelemetryManager.Instance.DefaultActivitySource.ShouldNotBeNull();
        bool activitySourceWasUnavailable = false;

        TelemetryManager.Instance.Dispose(
            () => activitySourceWasUnavailable = TelemetryManager.Instance.DefaultActivitySource is null,
            budgetMs: 1_000);

        activitySourceWasUnavailable.ShouldBeTrue();
    }
#endif

    [Fact]
    public void ResetForTestClearsDisposedState()
    {
        TelemetryManager.ResetForTest();
        TelemetryManager.IsStandaloneProcess = true;
        TelemetryManager.Instance.Dispose();
        TelemetryManager.IsDisposed.ShouldBeTrue();

        TelemetryManager.ResetForTest();

        TelemetryManager.IsDisposed.ShouldBeFalse();
        TelemetryManager.IsStandaloneProcess.ShouldBeFalse();
        TelemetryManager.Instance.DefaultActivitySource.ShouldBeNull();
    }

    private static void RecordCrash()
        => CrashTelemetryRecorder.RecordCrashTelemetry(
            new InvalidOperationException("The build crashed."),
            CrashExitType.UnhandledException,
            isUnhandled: true,
            isCritical: false);

    /// <summary>
    /// Gives a test a pristine <see cref="TelemetryManager"/> and a known consent state, and restores them afterwards.
    /// </summary>
    private sealed class TelemetryStateScope : IDisposable
    {
        private readonly TestEnvironment _environment = TestEnvironment.Create();

        public TelemetryStateScope(bool optedOut, bool isStandaloneProcess)
        {
            // Both switches are set because each target framework consults a different set of them, and so that the
            // tests don't depend on what the machine they run on has configured.
            string? optOut = optedOut ? "1" : null;
            _environment.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", optOut);
            _environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", optOut);
            Traits.UpdateFromEnvironment();

            KnownTelemetry.CrashTelemetry = null;
            TelemetryManager.ResetForTest();
            TelemetryManager.IsStandaloneProcess = isStandaloneProcess;
        }

        public void Dispose()
        {
            KnownTelemetry.CrashTelemetry = null;
            TelemetryManager.ResetForTest();
            _environment.Dispose();
            Traits.UpdateFromEnvironment();
        }
    }

#if NET
    /// <summary>
    /// Records the activities that the MSBuild telemetry activity source starts.
    /// </summary>
    private sealed class ActivityCapture : IDisposable
    {
        private readonly List<Activity> _started = [];
        private readonly ActivityListener _listener;

        public ActivityCapture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.DefaultActivitySourceNamespace, StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity =>
                {
                    lock (_started)
                    {
                        _started.Add(activity);
                    }
                },
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public IReadOnlyList<Activity> Started
        {
            get
            {
                lock (_started)
                {
                    return _started.ToArray();
                }
            }
        }

        public void Dispose() => _listener.Dispose();
    }
#endif
}
