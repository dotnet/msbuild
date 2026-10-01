// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#if NETFRAMEWORK
using System.Reflection;
using System.Runtime.Serialization;
#endif

namespace Microsoft.Build.Framework.UnitTests;

/// <summary>
/// Telemetry is best effort infrastructure - it must never be able to fail a build, and a process may only shut down
/// telemetry it owns. These tests use controlled sessions only; they never create a real telemetry transport.
/// </summary>
public sealed class TelemetryManager_Tests : IDisposable
{
    // Generous upper bound for "returned promptly" assertions, so that slow CI machines don't cause false failures while still
    // proving that a shutdown blocked forever does not block the caller.
    private static readonly TimeSpan PromptReturn = TimeSpan.FromSeconds(5);

    private readonly StringWriter _diagnostics = new();

    public TelemetryManager_Tests()
    {
        TelemetryManager.ResetForTest();
        TelemetryManager.DiagnosticsWriterForTest = _diagnostics;
    }

    public void Dispose()
    {
        TelemetryManager.ResetForTest();
        TelemetryManager.DiagnosticsWriterForTest = null;
    }

    /// <summary>
    /// Creates an environment that is not CI, has no diagnostics, and uses the default shutdown budget.
    /// </summary>
    private static TestEnvironment CreateEnvironment()
    {
        TestEnvironment env = TestEnvironment.Create();
        CIEnvironmentDetector_Tests.ClearCIEnvironment(env);
        env.SetEnvironmentVariable(TelemetryManager.ShutdownTimeoutEnvironmentVariable, null);
        env.SetEnvironmentVariable(TelemetryManager.DiagnosticsEnvironmentVariable, null);
        return env;
    }

    private sealed class RecordingShutdown
    {
        private int _calls;

        public int Calls => _calls;

        public bool? Transmit { get; private set; }

        public Task Invoke(bool transmit, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            Transmit = transmit;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void DisposeWithoutInitializeDoesNotThrow()
    {
        // On .NET Framework this reaches into the Visual Studio telemetry stack, which may not even be
        // loadable in the test host. Whatever it throws must not escape.
        Should.NotThrow(() => TelemetryManager.Instance.Dispose());

        TelemetryManager.IsDisposed.ShouldBeTrue();
        TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.NotOwned);
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        TelemetryManager.Instance.Dispose();
        Should.NotThrow(() => TelemetryManager.Instance.Dispose());

        TelemetryManager.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void ResetForTestClearsDisposedState()
    {
        TelemetryManager.Instance.Dispose();
        TelemetryManager.IsDisposed.ShouldBeTrue();

        TelemetryManager.ResetForTest();

        TelemetryManager.IsDisposed.ShouldBeFalse();
        TelemetryManager.Instance.DefaultActivitySource.ShouldBeNull();
    }

    [Fact]
    public void OwnedSessionIsShutDownOnceAndPersistedLocallyOutsideCI()
    {
        using TestEnvironment env = CreateEnvironment();
        RecordingShutdown shutdown = new();
        TelemetryManager.SetOwnedSessionShutdownForTest(shutdown.Invoke);

        TelemetryManager.Instance.Dispose();
        TelemetryManager.Instance.Dispose();

        shutdown.Calls.ShouldBe(1);
        shutdown.Transmit.ShouldBe(false);
        TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.Completed);
    }

    [Theory]
    [InlineData("TF_BUILD", "True")]
    [InlineData("GITHUB_ACTIONS", "true")]
    [InlineData("CI", "true")]
    public void OwnedSessionIsTransmittedOnCI(string variable, string value)
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable(variable, value);
        RecordingShutdown shutdown = new();
        TelemetryManager.SetOwnedSessionShutdownForTest(shutdown.Invoke);

        TelemetryManager.Instance.Dispose();

        shutdown.Calls.ShouldBe(1);
        shutdown.Transmit.ShouldBe(true);
        TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.Completed);
    }

    [Fact]
    public void ShutdownIsBoundedWhenTransportIgnoresCancellation()
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable("TF_BUILD", "True");
        env.SetEnvironmentVariable(TelemetryManager.ShutdownTimeoutEnvironmentVariable, "200");

        // Simulates a transport that is stuck in a blocking call and never observes cancellation.
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TelemetryManager.SetOwnedSessionShutdownForTest((_, _) =>
        {
            release.Task.Wait(CancellationToken.None);
            return Task.CompletedTask;
        });

        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Should.NotThrow(() => TelemetryManager.Instance.Dispose());
            stopwatch.Stop();

            TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.TimedOut);
            stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(150));
            stopwatch.Elapsed.ShouldBeLessThan(PromptReturn);
        }
        finally
        {
            release.TrySetResult(true);
        }
    }

    [Fact]
    public void ShutdownSignalsCancellationWhenBudgetIsExhausted()
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable("TF_BUILD", "True");
        env.SetEnvironmentVariable(TelemetryManager.ShutdownTimeoutEnvironmentVariable, "100");

        // Simulates an unreachable collector: the upload only ends when it is cancelled.
        TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TelemetryManager.SetOwnedSessionShutdownForTest((_, cancellationToken) =>
        {
            cancellationToken.Register(() => cancelled.TrySetResult(true));
            return Task.Delay(Timeout.Infinite, cancellationToken);
        });

        Stopwatch stopwatch = Stopwatch.StartNew();
        TelemetryManager.Instance.Dispose();
        stopwatch.Stop();

        TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.TimedOut);
        stopwatch.Elapsed.ShouldBeLessThan(PromptReturn);
        cancelled.Task.Wait(PromptReturn).ShouldBeTrue();
    }

    [Fact]
    public void ZeroBudgetDoesNotWait()
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable(TelemetryManager.ShutdownTimeoutEnvironmentVariable, "0");

        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TelemetryManager.SetOwnedSessionShutdownForTest((_, _) =>
        {
            release.Task.Wait(CancellationToken.None);
            return Task.CompletedTask;
        });

        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            TelemetryManager.Instance.Dispose();
            stopwatch.Stop();

            TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.TimedOut);
            stopwatch.Elapsed.ShouldBeLessThan(PromptReturn);
        }
        finally
        {
            release.TrySetResult(true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShutdownFailureIsSwallowedAndReportedWithoutMessage(bool throwSynchronously)
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable(TelemetryManager.DiagnosticsEnvironmentVariable, "1");

        const string SensitiveMessage = @"C:\Users\someone\secret";
        TelemetryManager.SetOwnedSessionShutdownForTest((_, _) =>
            throwSynchronously
                ? throw new InvalidOperationException(SensitiveMessage)
                : Task.FromException(new InvalidOperationException(SensitiveMessage)));

        Should.NotThrow(() => TelemetryManager.Instance.Dispose());

        TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.Failed);
        string diagnostics = _diagnostics.ToString();
        diagnostics.ShouldContain("failed: System.InvalidOperationException");
        diagnostics.ShouldNotContain(SensitiveMessage);
    }

    [Fact]
    public void ShutdownDoesNotHoldTheTelemetryLock()
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable(TelemetryManager.ShutdownTimeoutEnvironmentVariable, "5000");

        // The shutdown runs on another thread and needs the telemetry lock. If Dispose waited while holding the lock,
        // this would only end when the budget is exhausted.
        TelemetryManager.SetOwnedSessionShutdownForTest((_, _) =>
        {
            TelemetryManager.Instance.Initialize(isStandalone: true);
            return Task.CompletedTask;
        });

        TelemetryManager.Instance.Dispose();

        TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.Completed);
    }

    [Fact]
    public void DiagnosticsReportShutdownWhenEnabled()
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable("TF_BUILD", "True");
        env.SetEnvironmentVariable(TelemetryManager.DiagnosticsEnvironmentVariable, "1");
        TelemetryManager.SetOwnedSessionShutdownForTest(new RecordingShutdown().Invoke);

        TelemetryManager.Instance.Dispose();

        _diagnostics.ToString().ShouldContain("MSBuild telemetry: shutdown (transmit to network (CI)) completed after");
    }

    [Fact]
    public void DiagnosticsAreQuietByDefault()
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable(TelemetryManager.OptOutEnvironmentVariable, "1");
        env.SetEnvironmentVariable(TelemetryManager.ShutdownTimeoutEnvironmentVariable, "not a number");

        TelemetryManager.Instance.Initialize(isStandalone: true);
        TelemetryManager.GetShutdownTimeout();
        TelemetryManager.SetOwnedSessionShutdownForTest((_, _) => throw new InvalidOperationException());
        TelemetryManager.Instance.Dispose();

        TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.Failed);
        _diagnostics.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void OptOutPreventsInitialization()
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable(TelemetryManager.OptOutEnvironmentVariable, "1");
        env.SetEnvironmentVariable(TelemetryManager.DiagnosticsEnvironmentVariable, "1");
        bool initializerCalled = false;
        TelemetryManager.InitializerForTest = _ =>
        {
            initializerCalled = true;
            return null;
        };

        TelemetryManager.Instance.Initialize(isStandalone: true);

        initializerCalled.ShouldBeFalse();
        TelemetryManager.Instance.DefaultActivitySource.ShouldBeNull();
        _diagnostics.ToString().ShouldContain($"disabled by {TelemetryManager.OptOutEnvironmentVariable}");

        // Opting out must not depend on CI detection.
        env.SetEnvironmentVariable("TF_BUILD", "True");
        TelemetryManager.Instance.Dispose();
        TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.NotOwned);
    }

    [Fact]
    public void InitializationFailureDisablesTelemetryWithoutThrowing()
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable(TelemetryManager.OptOutEnvironmentVariable, null);
        env.SetEnvironmentVariable(TelemetryManager.DiagnosticsEnvironmentVariable, "1");
        int initializerCalls = 0;
        TelemetryManager.InitializerForTest = _ =>
        {
            initializerCalls++;
            throw new FileNotFoundException(
                @"Could not load C:\Users\someone\secret",
                "Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        };

        Should.NotThrow(() => TelemetryManager.Instance.Initialize(isStandalone: true));
        Should.NotThrow(() => TelemetryManager.Instance.Initialize(isStandalone: true));

        initializerCalls.ShouldBe(1);
        TelemetryManager.Instance.DefaultActivitySource.ShouldBeNull();
        string diagnostics = _diagnostics.ToString();
        diagnostics.ShouldContain("initialization failed");
        diagnostics.ShouldContain("System.IO.FileNotFoundException");
        diagnostics.ShouldContain("'Microsoft.VisualStudio.Telemetry'");
        diagnostics.ShouldNotContain("secret");

        TelemetryManager.Instance.Dispose();
        TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.NotOwned);
    }

    [Fact]
    public void InitializeAfterDisposeIsIgnored()
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable(TelemetryManager.OptOutEnvironmentVariable, null);
        bool initializerCalled = false;
        TelemetryManager.InitializerForTest = _ =>
        {
            initializerCalled = true;
            return null;
        };

        TelemetryManager.Instance.Dispose();
        TelemetryManager.Instance.Initialize(isStandalone: true);

        initializerCalled.ShouldBeFalse();
    }

    [Theory]
    [InlineData("2500", 2500)]
    [InlineData(" 2500 ", 2500)]
    [InlineData("0", 0)]
    public void ShutdownTimeoutCanBeConfigured(string value, int expectedMilliseconds)
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable(TelemetryManager.ShutdownTimeoutEnvironmentVariable, value);

        TelemetryManager.GetShutdownTimeout().ShouldBe(TimeSpan.FromMilliseconds(expectedMilliseconds));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("1e3")]
    [InlineData("99999999999")]
    public void InvalidShutdownTimeoutFallsBackToDefault(string value)
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable(TelemetryManager.ShutdownTimeoutEnvironmentVariable, value);
        env.SetEnvironmentVariable(TelemetryManager.DiagnosticsEnvironmentVariable, "1");

        TelemetryManager.GetShutdownTimeout().ShouldBe(TelemetryManager.DefaultShutdownTimeout);
        _diagnostics.ToString().ShouldContain($"ignoring {TelemetryManager.ShutdownTimeoutEnvironmentVariable}");
    }

    [Fact]
    public void DefaultShutdownTimeoutIsUsedWhenNotConfigured()
    {
        using TestEnvironment env = CreateEnvironment();

        TelemetryManager.GetShutdownTimeout().ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData("Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    [InlineData(@"C:\Users\someone\secret\Microsoft.VisualStudio.Telemetry.dll")]
    [InlineData("file:///C:/Users/someone/secret/Microsoft.VisualStudio.Telemetry.dll")]
    public void DescribeExceptionOmitsMessagesAndPaths(string fileName)
    {
        FileLoadException exception = new(@"Could not load C:\Users\someone\secret", fileName);

        string description = TelemetryManager.DescribeException(new TypeInitializationException("SomeType", exception));

        description.ShouldContain("System.TypeInitializationException");
        description.ShouldContain("caused by System.IO.FileLoadException");
        description.ShouldContain("for assembly 'Microsoft.VisualStudio.Telemetry'");
        description.ShouldNotContain("someone");
        description.ShouldNotContain("secret");
        description.ShouldNotContain("Version=");
    }

#if NETFRAMEWORK
    private static (FieldInfo Session, FieldInfo Ownership, Type SessionType) GetVsTelemetryState()
    {
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

        return (sessionField, ownershipField, telemetrySessionType);
    }

    [Fact]
    public void DisposeSwallowsTelemetrySessionNullReferenceAndClearsState()
    {
        using TestEnvironment env = CreateEnvironment();
        (FieldInfo sessionField, FieldInfo ownershipField, Type telemetrySessionType) = GetVsTelemetryState();

        MethodInfo disposeMethod = telemetrySessionType.GetMethod(nameof(IDisposable.Dispose), Type.EmptyTypes)
            ?? throw new InvalidOperationException("TelemetrySession.Dispose was not found.");

        object controlSession = FormatterServices.GetUninitializedObject(telemetrySessionType);
        TargetInvocationException controlException = Should.Throw<TargetInvocationException>(
            () => disposeMethod.Invoke(controlSession, null));
        controlException.InnerException.ShouldBeOfType<NullReferenceException>();

        // An owned session whose initialization failed partway is still shut down, and the failure is swallowed.
        sessionField.SetValue(null, FormatterServices.GetUninitializedObject(telemetrySessionType));
        ownershipField.SetValue(null, true);

        try
        {
            Should.NotThrow(() => TelemetryManager.Instance.Dispose());

            TelemetryManager.IsDisposed.ShouldBeTrue();
            TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.Failed);
            sessionField.GetValue(null).ShouldBeNull();
            ownershipField.GetValue(null).ShouldBe(false);
        }
        finally
        {
            sessionField.SetValue(null, null);
            ownershipField.SetValue(null, false);
        }
    }

    [Fact]
    public void HostSessionIsNeverShutDown()
    {
        using TestEnvironment env = CreateEnvironment();
        env.SetEnvironmentVariable("TF_BUILD", "True");
        (FieldInfo sessionField, FieldInfo ownershipField, Type telemetrySessionType) = GetVsTelemetryState();

        // A session borrowed from a host such as Visual Studio. Shutting it down would throw, which would report Failed.
        sessionField.SetValue(null, FormatterServices.GetUninitializedObject(telemetrySessionType));
        ownershipField.SetValue(null, false);

        try
        {
            TelemetryManager.Instance.Dispose();

            TelemetryManager.LastShutdownOutcome.ShouldBe(TelemetryShutdownOutcome.NotOwned);
            sessionField.GetValue(null).ShouldBeNull();
        }
        finally
        {
            sessionField.SetValue(null, null);
            ownershipField.SetValue(null, false);
        }
    }
#endif
}
