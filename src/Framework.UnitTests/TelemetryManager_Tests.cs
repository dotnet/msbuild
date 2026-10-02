// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;
using System.Reflection;
using System.Runtime.Serialization;
#endif

using System.IO;
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

#if NETFRAMEWORK
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

    [Theory]
    [InlineData("Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    [InlineData(@"C:\Users\someone\secret\Microsoft.VisualStudio.Telemetry.dll")]
    [InlineData("file:///C:/Users/someone/secret/Microsoft.VisualStudio.Telemetry.dll")]
    public void DescribeExceptionOmitsMessagesAndPaths(string fileName)
    {
        FileLoadException exception = new(@"Could not load C:\Users\someone\secret", fileName);

        string description = TelemetryManager.DescribeException(new System.TypeInitializationException("SomeType", exception));

        description.ShouldContain("System.TypeInitializationException");
        description.ShouldContain("caused by System.IO.FileLoadException");
        description.ShouldContain("for assembly 'Microsoft.VisualStudio.Telemetry'");
        description.ShouldNotContain("someone");
        description.ShouldNotContain("secret");
        description.ShouldNotContain("Version=");
    }
}
