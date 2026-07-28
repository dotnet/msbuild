// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework.Telemetry;
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
