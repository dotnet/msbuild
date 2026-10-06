// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
#endif

using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

/// <summary>
/// Telemetry is best effort infrastructure - it must never be able to fail a build.
/// </summary>
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

        bool completed = VsTelemetryInitializer.DisposeOwnedSession(
            upload: true,
            timeoutMs: 1_000,
            disposeToNetworkAsync: () =>
            {
                uploadCalled = true;
                return Task.CompletedTask;
            },
            dispose: () => disposeCalled = true);

        completed.ShouldBeTrue();
        uploadCalled.ShouldBeTrue();
        disposeCalled.ShouldBeFalse();
    }

    [Fact]
    public void OwnedSessionOutsideCiUsesLocalDisposal()
    {
        bool uploadCalled = false;
        bool disposeCalled = false;

        bool completed = VsTelemetryInitializer.DisposeOwnedSession(
            upload: false,
            timeoutMs: 1_000,
            disposeToNetworkAsync: () =>
            {
                uploadCalled = true;
                return Task.CompletedTask;
            },
            dispose: () => disposeCalled = true);

        completed.ShouldBeTrue();
        uploadCalled.ShouldBeFalse();
        disposeCalled.ShouldBeTrue();
    }

    [Fact]
    public void OwnedSessionShutdownHonorsConfiguredTimeout()
    {
        const int timeoutMs = 50;
        TaskCompletionSource<object?> uploadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Stopwatch stopwatch = Stopwatch.StartNew();

        bool completed;
        try
        {
            completed = VsTelemetryInitializer.DisposeOwnedSession(
                upload: true,
                timeoutMs,
                () => uploadCompletion.Task,
                () => throw new InvalidOperationException("The local disposal path must not run in CI."));
        }
        finally
        {
            uploadCompletion.TrySetResult(null);
        }

        stopwatch.Stop();
        completed.ShouldBeFalse();
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(timeoutMs / 2);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2_000);
    }

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
    public void ResetForTestClearsDisposedState()
    {
        TelemetryManager.ResetForTest();
        TelemetryManager.Instance.Dispose();
        TelemetryManager.IsDisposed.ShouldBeTrue();

        TelemetryManager.ResetForTest();

        TelemetryManager.IsDisposed.ShouldBeFalse();
        TelemetryManager.Instance.DefaultActivitySource.ShouldBeNull();
    }
}
