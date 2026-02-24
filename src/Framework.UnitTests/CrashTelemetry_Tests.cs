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
            ExitType = CrashExitType.Unexpected,
            IsCritical = false,
            IsUnhandled = true,
            StackHash = "ABC123",
            StackTop = "at Foo.Bar()",
            HResult = -2147024809,
            BuildEngineVersion = "17.0.0",
            BuildEngineFrameworkName = ".NET 10.0",
            BuildEngineHost = "VS",
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
            ExitType = CrashExitType.Unexpected,
            IsCritical = true,
            IsUnhandled = false,
            StackHash = "DEF456",
            StackTop = "at Foo.Baz()",
            HResult = -1,
            BuildEngineVersion = "17.0.0",
            BuildEngineFrameworkName = ".NET 10.0",
            BuildEngineHost = "CLI",
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
    }

    [Fact]
    public void EventName_IsCrash()
    {
        CrashTelemetry telemetry = new();
        telemetry.EventName.ShouldBe("crash");
    }

    [Fact]
    public void PopulateFromException_SetsInnermostExceptionType_ForNestedExceptions()
    {
        var root = new IOException("root cause");
        var mid = new InvalidOperationException("mid", root);
        var outer = new TypeInitializationException("SomeType", mid);

        CrashTelemetry telemetry = new();

        try
        {
            throw outer;
        }
        catch (Exception ex)
        {
            telemetry.PopulateFromException(ex);
        }

        telemetry.ExceptionType.ShouldBe("System.TypeInitializationException");
        telemetry.InnerExceptionType.ShouldBe("System.InvalidOperationException");
        telemetry.InnermostExceptionType.ShouldBe("System.IO.IOException");
    }

    [Fact]
    public void PopulateFromException_InnermostExceptionType_IsNull_WhenNoInnerException()
    {
        CrashTelemetry telemetry = new();

        try
        {
            throw new ArgumentException("no inner");
        }
        catch (Exception ex)
        {
            telemetry.PopulateFromException(ex);
        }

        telemetry.InnermostExceptionType.ShouldBeNull();
    }

    [Fact]
    public void PopulateFromException_InnermostExceptionType_EqualsInner_WhenSingleInner()
    {
        var inner = new ArgumentException("inner");
        var outer = new InvalidOperationException("outer", inner);

        CrashTelemetry telemetry = new();

        try
        {
            throw outer;
        }
        catch (Exception ex)
        {
            telemetry.PopulateFromException(ex);
        }

        // When there's only one inner, InnermostExceptionType should equal InnerExceptionType.
        telemetry.InnerExceptionType.ShouldBe("System.ArgumentException");
        telemetry.InnermostExceptionType.ShouldBe("System.ArgumentException");
    }

    [Fact]
    public void PopulateFromException_SetsCrashOriginToMSBuild_WhenExceptionFromMSBuildCode()
    {
        CrashTelemetry telemetry = new();

        // Throw from this test, which lives in a Microsoft.Build.* namespace.
        try
        {
            throw new InvalidOperationException("test");
        }
        catch (Exception ex)
        {
            telemetry.PopulateFromException(ex);
        }

        telemetry.CrashOrigin.ShouldBe(CrashOriginKind.MSBuild);
        telemetry.CrashOriginAssembly.ShouldNotBeNull();
        telemetry.CrashOriginAssembly.ShouldStartWith("Microsoft.Build");
    }

    [Theory]
    [InlineData("Microsoft.Build.BackEnd.SomeClass.Method", "Microsoft.Build.BackEnd")]
    [InlineData("Microsoft.Build.Evaluation.ProjectParser.Parse", "Microsoft.Build.Evaluation")]
    [InlineData("Microsoft.Build.Execution.BuildManager.Build", "Microsoft.Build.Execution")]
    [InlineData("Microsoft.VisualStudio.RemoteControl.RemoteControlClient.GetFileAsync", "Microsoft.VisualStudio.RemoteControl")]
    [InlineData("System.IO.File.ReadAllText", "System.IO")]
    [InlineData("Newtonsoft.Json.JsonConvert.DeserializeObject", "Newtonsoft.Json")]
    public void ExtractOriginNamespace_ExtractsCorrectNamespace(string qualifiedMethod, string expectedNamespace)
    {
        // Build a fake stack trace with the given qualified method.
        string fakeStack = $"   at {qualifiedMethod}(String arg)";
        Exception ex = CreateExceptionWithStack(fakeStack);

        string? result = CrashTelemetry.ExtractOriginNamespace(ex);

        result.ShouldBe(expectedNamespace);
    }

    [Theory]
    [InlineData("Microsoft.Build.BackEnd", CrashOriginKind.MSBuild)]
    [InlineData("Microsoft.Build.Evaluation", CrashOriginKind.MSBuild)]
    [InlineData("Microsoft.Build", CrashOriginKind.MSBuild)]
    [InlineData("Microsoft.VisualStudio.RemoteControl", CrashOriginKind.ThirdParty)]
    [InlineData("System.IO", CrashOriginKind.ThirdParty)]
    [InlineData("Newtonsoft.Json", CrashOriginKind.ThirdParty)]
    [InlineData(null, CrashOriginKind.Unknown)]
    [InlineData("", CrashOriginKind.Unknown)]
    public void ClassifyOrigin_ReturnsCorrectCategory(string? originNamespace, CrashOriginKind expectedOrigin)
    {
        CrashOriginKind result = CrashTelemetry.ClassifyOrigin(originNamespace);
        result.ShouldBe(expectedOrigin);
    }

    [Fact]
    public void ExtractOriginNamespace_ReturnsNull_WhenNoStackTrace()
    {
        // An exception that was never thrown has no stack trace.
        Exception ex = new Exception("no stack");
        string? result = CrashTelemetry.ExtractOriginNamespace(ex);
        result.ShouldBeNull();
    }

    [Fact]
    public void GetProperties_IncludesNewFields()
    {
        CrashTelemetry telemetry = new()
        {
            ExceptionType = "System.TypeInitializationException",
            IsUnhandled = true,
            CrashOrigin = CrashOriginKind.MSBuild,
            CrashOriginAssembly = "Microsoft.Build.BackEnd",
            InnermostExceptionType = "System.IO.IOException",
        };

        IDictionary<string, string> props = telemetry.GetProperties();
        props[nameof(CrashTelemetry.CrashOrigin)].ShouldBe("MSBuild");
        props[nameof(CrashTelemetry.CrashOriginAssembly)].ShouldBe("Microsoft.Build.BackEnd");
        props[nameof(CrashTelemetry.InnermostExceptionType)].ShouldBe("System.IO.IOException");
    }

    [Fact]
    public void GetActivityProperties_IncludesNewFields()
    {
        CrashTelemetry telemetry = new()
        {
            ExceptionType = "System.OutOfMemoryException",
            IsUnhandled = true,
            CrashOrigin = CrashOriginKind.ThirdParty,
            CrashOriginAssembly = "Microsoft.VisualStudio.RemoteControl",
            InnermostExceptionType = "System.OutOfMemoryException",
        };

        Dictionary<string, object> props = telemetry.GetActivityProperties();
        props[nameof(CrashTelemetry.CrashOrigin)].ShouldBe("ThirdParty");
        props[nameof(CrashTelemetry.CrashOriginAssembly)].ShouldBe("Microsoft.VisualStudio.RemoteControl");
        props[nameof(CrashTelemetry.InnermostExceptionType)].ShouldBe("System.OutOfMemoryException");
    }

    /// <summary>
    /// Creates an exception whose StackTrace property returns the given fake stack string.
    /// </summary>
    private static Exception CreateExceptionWithStack(string fakeStack)
    {
        return new ExceptionWithFakeStack(fakeStack);
    }

    private sealed class ExceptionWithFakeStack : Exception
    {
        private readonly string _stack;

        public ExceptionWithFakeStack(string stack) : base("fake")
        {
            _stack = stack;
        }

        public override string? StackTrace => _stack;
    }
}
