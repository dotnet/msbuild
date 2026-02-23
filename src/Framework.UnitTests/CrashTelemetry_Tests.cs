// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework.Telemetry;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class CrashTelemetry_Tests
{
    [Fact]
    public void PopulateFromException_SetsAllFields()
    {
        var inner = new ArgumentException("inner");
        var outer = new InvalidOperationException("outer", inner);

        CrashTelemetry telemetry = new();

        // Throw and catch to get a real stack trace.
        try
        {
            throw outer;
        }
        catch (Exception ex)
        {
            telemetry.PopulateFromException(ex);
        }

        telemetry.ExceptionType.ShouldBe("System.InvalidOperationException");
        telemetry.InnerExceptionType.ShouldBe("System.ArgumentException");
        telemetry.HResult.ShouldNotBeNull();
        telemetry.CrashTimestamp.ShouldNotBeNull();
        telemetry.StackHash.ShouldNotBeNull();
        telemetry.StackTop.ShouldNotBeNull();
    }

    [Fact]
    public void PopulateFromException_NoInnerException_SetsInnerToNull()
    {
        CrashTelemetry telemetry = new();

        try
        {
            throw new FileNotFoundException("not found");
        }
        catch (Exception ex)
        {
            telemetry.PopulateFromException(ex);
        }

        telemetry.ExceptionType.ShouldBe("System.IO.FileNotFoundException");
        telemetry.InnerExceptionType.ShouldBeNull();
    }

    [Fact]
    public void StackHash_IsDeterministic()
    {
        CrashTelemetry t1 = new();
        CrashTelemetry t2 = new();

        try
        {
            throw new Exception("test");
        }
        catch (Exception ex)
        {
            t1.PopulateFromException(ex);
            t2.PopulateFromException(ex);
        }

        t1.StackHash.ShouldBe(t2.StackHash);
    }

    [Fact]
    public void StackTop_RedactsFilePaths()
    {
        CrashTelemetry telemetry = new();

        try
        {
            throw new Exception("test");
        }
        catch (Exception ex)
        {
            telemetry.PopulateFromException(ex);
        }

        // In debug builds, the stack trace includes file paths.
        // StackTop should have " in <redacted>:line " instead of the real path.
        string? stackTop = telemetry.StackTop;
        stackTop.ShouldNotBeNull();
        stackTop.ShouldNotContain(nameof(CrashTelemetry_Tests) + ".cs");
    }

    [Fact]
    public void GetProperties_IncludesAllSetFields()
    {
        CrashTelemetry telemetry = new()
        {
            ExceptionType = "System.InvalidOperationException",
            InnerExceptionType = "System.ArgumentException",
            ExitType = "Unexpected",
            IsCritical = false,
            IsUnhandled = true,
            StackHash = "ABC123",
            StackTop = "at Foo.Bar()",
            HResult = -2147024809,
            BuildEngineVersion = "17.0.0",
            BuildEngineFrameworkName = ".NET 10.0",
            BuildEngineHost = "VS",
            CrashTimestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        IDictionary<string, string> props = telemetry.GetProperties();
        props[nameof(CrashTelemetry.ExceptionType)].ShouldBe("System.InvalidOperationException");
        props[nameof(CrashTelemetry.InnerExceptionType)].ShouldBe("System.ArgumentException");
        props[nameof(CrashTelemetry.ExitType)].ShouldBe("Unexpected");
        props[nameof(CrashTelemetry.IsCritical)].ShouldBe("False");
        props[nameof(CrashTelemetry.IsUnhandled)].ShouldBe("True");
        props[nameof(CrashTelemetry.StackHash)].ShouldBe("ABC123");
        props[nameof(CrashTelemetry.StackTop)].ShouldBe("at Foo.Bar()");
        props[nameof(CrashTelemetry.HResult)].ShouldBe("-2147024809");
        props[nameof(CrashTelemetry.BuildEngineVersion)].ShouldBe("17.0.0");
        props[nameof(CrashTelemetry.BuildEngineFrameworkName)].ShouldBe(".NET 10.0");
        props[nameof(CrashTelemetry.BuildEngineHost)].ShouldBe("VS");
        props[nameof(CrashTelemetry.CrashTimestamp)].ShouldBe("2025-01-01T00:00:00.0000000Z");
    }

    [Fact]
    public void GetProperties_OmitsNullFields()
    {
        CrashTelemetry telemetry = new()
        {
            ExceptionType = "System.Exception",
            IsUnhandled = false,
        };

        IDictionary<string, string> props = telemetry.GetProperties();
        props.ShouldContainKey(nameof(CrashTelemetry.ExceptionType));
        props.ShouldContainKey(nameof(CrashTelemetry.IsUnhandled));
        props.ShouldNotContainKey(nameof(CrashTelemetry.InnerExceptionType));
        props.ShouldNotContainKey(nameof(CrashTelemetry.StackHash));
        props.ShouldNotContainKey(nameof(CrashTelemetry.BuildEngineHost));
    }

    [Fact]
    public void GetActivityProperties_IncludesAllSetFields()
    {
        CrashTelemetry telemetry = new()
        {
            ExceptionType = "System.InvalidOperationException",
            ExitType = "Unexpected",
            IsCritical = true,
            IsUnhandled = false,
            StackHash = "DEF456",
            StackTop = "at Foo.Baz()",
            HResult = -1,
            BuildEngineVersion = "17.0.0",
            BuildEngineFrameworkName = ".NET 10.0",
            BuildEngineHost = "CLI",
            CrashTimestamp = new DateTime(2025, 6, 15, 12, 30, 0, DateTimeKind.Utc),
        };

        Dictionary<string, object> props = telemetry.GetActivityProperties();
        props[nameof(CrashTelemetry.ExceptionType)].ShouldBe("System.InvalidOperationException");
        props[nameof(CrashTelemetry.ExitType)].ShouldBe("Unexpected");
        props[nameof(CrashTelemetry.IsCritical)].ShouldBe(true);
        props[nameof(CrashTelemetry.IsUnhandled)].ShouldBe(false);
        props[nameof(CrashTelemetry.StackHash)].ShouldBe("DEF456");
        props[nameof(CrashTelemetry.StackTop)].ShouldBe("at Foo.Baz()");
        props[nameof(CrashTelemetry.HResult)].ShouldBe(-1);
        props[nameof(CrashTelemetry.BuildEngineVersion)].ShouldBe("17.0.0");
        props[nameof(CrashTelemetry.BuildEngineFrameworkName)].ShouldBe(".NET 10.0");
        props[nameof(CrashTelemetry.BuildEngineHost)].ShouldBe("CLI");
        props[nameof(CrashTelemetry.CrashTimestamp)].ShouldBe("2025-06-15T12:30:00.0000000Z");
    }

    [Fact]
    public void EventName_IsCrash()
    {
        CrashTelemetry telemetry = new();
        telemetry.EventName.ShouldBe("crash");
    }
}
