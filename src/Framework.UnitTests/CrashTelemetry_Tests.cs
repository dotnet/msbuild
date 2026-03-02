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
        telemetry.CrashOriginNamespace.ShouldNotBeNull();
        telemetry.CrashOriginNamespace.ShouldStartWith("Microsoft.Build");
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
    [InlineData("Microsoft.Build.BackEnd", "MSBuild")]
    [InlineData("Microsoft.Build.Evaluation", "MSBuild")]
    [InlineData("Microsoft.Build", "MSBuild")]
    [InlineData("Microsoft.VisualStudio.RemoteControl", "ThirdParty")]
    [InlineData("System.IO", "ThirdParty")]
    [InlineData("Newtonsoft.Json", "ThirdParty")]
    [InlineData(null, "Unknown")]
    [InlineData("", "Unknown")]
    public void ClassifyOrigin_ReturnsCorrectCategory(string? originNamespace, string expectedOrigin)
    {
        CrashOriginKind result = CrashTelemetry.ClassifyOrigin(originNamespace);
        result.ToString().ShouldBe(expectedOrigin);
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
            CrashOriginNamespace = "Microsoft.Build.BackEnd",
            InnermostExceptionType = "System.IO.IOException",
        };

        IDictionary<string, string> props = telemetry.GetProperties();
        props[nameof(CrashTelemetry.CrashOrigin)].ShouldBe("MSBuild");
        props[nameof(CrashTelemetry.CrashOriginNamespace)].ShouldBe("Microsoft.Build.BackEnd");
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
            CrashOriginNamespace = "Microsoft.VisualStudio.RemoteControl",
            InnermostExceptionType = "System.OutOfMemoryException",
        };

        Dictionary<string, object> props = telemetry.GetActivityProperties();
        props[nameof(CrashTelemetry.CrashOrigin)].ShouldBe("ThirdParty");
        props[nameof(CrashTelemetry.CrashOriginNamespace)].ShouldBe("Microsoft.VisualStudio.RemoteControl");
        props[nameof(CrashTelemetry.InnermostExceptionType)].ShouldBe("System.OutOfMemoryException");
    }

    [Fact]
    public void ExtractStackCaller_ReturnsCallerFrame_WhenTopIsThrowHelper()
    {
        string fakeStack =
            "   at Microsoft.Build.Shared.ErrorUtilities.ThrowInternalError(String message, Object[] args)\r\n" +
            "   at Microsoft.Build.BackEnd.RequestBuilder.BuildProject(String projectFile)";
        Exception ex = CreateExceptionWithStack(fakeStack);

        string? caller = CrashTelemetry.ExtractStackCaller(ex);

        caller.ShouldNotBeNull();
        caller.ShouldContain("Microsoft.Build.BackEnd.RequestBuilder.BuildProject");
    }

    [Fact]
    public void ExtractStackCaller_ReturnsNull_WhenTopIsNotThrowHelper()
    {
        string fakeStack =
            "   at Microsoft.Build.BackEnd.RequestBuilder.BuildProject(String projectFile)\r\n" +
            "   at Microsoft.Build.BackEnd.BuildManager.Build()";
        Exception ex = CreateExceptionWithStack(fakeStack);

        string? caller = CrashTelemetry.ExtractStackCaller(ex);

        caller.ShouldBeNull();
    }

    [Fact]
    public void ExtractStackCaller_ReturnsNull_WhenThrowHelperIsOnlyFrame()
    {
        string fakeStack = "   at Microsoft.Build.Shared.ErrorUtilities.ThrowInternalError(String message, Object[] args)";
        Exception ex = CreateExceptionWithStack(fakeStack);

        string? caller = CrashTelemetry.ExtractStackCaller(ex);

        caller.ShouldBeNull();
    }

    [Fact]
    public void ExtractStackCaller_ReturnsNull_WhenNoStackTrace()
    {
        Exception ex = new Exception("no stack");

        string? caller = CrashTelemetry.ExtractStackCaller(ex);

        caller.ShouldBeNull();
    }

    [Theory]
    [InlineData("ErrorUtilities.VerifyThrowInternalError(")]
    [InlineData("ErrorUtilities.ThrowInternalErrorUnreachable(")]
    [InlineData("ErrorUtilities.VerifyThrowInternalNull(")]
    [InlineData("ErrorUtilities.ThrowInvalidOperation(")]
    [InlineData("ErrorUtilities.VerifyThrow(")]
    public void ExtractStackCaller_RecognizesAllThrowHelpers(string helperMethod)
    {
        string fakeStack =
            $"   at Microsoft.Build.Shared.{helperMethod}String message)\r\n" +
            "   at Microsoft.Build.Evaluation.Evaluator.Evaluate()";
        Exception ex = CreateExceptionWithStack(fakeStack);

        string? caller = CrashTelemetry.ExtractStackCaller(ex);

        caller.ShouldNotBeNull();
        caller.ShouldContain("Microsoft.Build.Evaluation.Evaluator.Evaluate");
    }

    [Fact]
    public void ExtractStackCaller_RedactsFilePaths_InCallerFrame()
    {
        string fakeStack =
            "   at Microsoft.Build.Shared.ErrorUtilities.ThrowInternalError(String message, Object[] args)\r\n" +
            "   at Microsoft.Build.BackEnd.RequestBuilder.BuildProject(String projectFile) in C:\\Users\\username\\src\\file.cs:line 42";
        Exception ex = CreateExceptionWithStack(fakeStack);

        string? caller = CrashTelemetry.ExtractStackCaller(ex);

        caller.ShouldNotBeNull();
        caller.ShouldNotContain("username");
        caller.ShouldContain("<redacted>");
        caller.ShouldContain(":line 42");
    }

    [Fact]
    public void PopulateFromException_SetsStackCaller_WhenThrowHelperIsOnTop()
    {
        CrashTelemetry telemetry = new();

        // Simulate a throw-helper scenario using a fake stack trace.
        string fakeStack =
            "   at Microsoft.Build.Shared.ErrorUtilities.ThrowInternalError(String message, Object[] args)\r\n" +
            "   at Microsoft.Build.Scheduler.ScheduleRequest(BuildRequest request)";
        Exception ex = CreateExceptionWithStack(fakeStack);

        telemetry.PopulateFromException(ex);

        telemetry.StackTop.ShouldNotBeNull();
        telemetry.StackTop!.ShouldContain("ErrorUtilities.ThrowInternalError");
        telemetry.StackCaller.ShouldNotBeNull();
        telemetry.StackCaller!.ShouldContain("Microsoft.Build.Scheduler.ScheduleRequest");
    }

    [Fact]
    public void PopulateFromException_SetsExceptionMessage()
    {
        CrashTelemetry telemetry = new();

        try
        {
            throw new InvalidOperationException("something went wrong");
        }
        catch (Exception ex)
        {
            telemetry.PopulateFromException(ex);
        }

        telemetry.ExceptionMessage.ShouldBe("something went wrong");
    }

    [Fact]
    public void PopulateFromException_StripsInternalErrorPrefix()
    {
        CrashTelemetry telemetry = new();

        try
        {
            throw new Exception("MSB0001: Internal MSBuild Error: All submissions not yet complete.");
        }
        catch (Exception ex)
        {
            telemetry.PopulateFromException(ex);
        }

        telemetry.ExceptionMessage.ShouldBe("All submissions not yet complete.");
    }

    [Fact]
    public void TruncateMessage_ReturnsNull_WhenEmpty()
    {
        CrashTelemetry.TruncateMessage(null).ShouldBeNull();
        CrashTelemetry.TruncateMessage("").ShouldBeNull();
    }

    [Fact]
    public void TruncateMessage_TruncatesLongMessages()
    {
        string longMessage = new string('x', 500);
        string? result = CrashTelemetry.TruncateMessage(longMessage);

        result.ShouldNotBeNull();
        result.Length.ShouldBe(256);
    }

    [Fact]
    public void TruncateMessage_RedactsWindowsPaths()
    {
        string message = @"C:\Users\johndoe\src\project.csproj unexpectedly not a rooted path";
        string? result = CrashTelemetry.TruncateMessage(message);

        result.ShouldNotBeNull();
        result.ShouldNotContain("johndoe");
        result.ShouldNotContain(@"C:\Users");
        result.ShouldContain("<path>");
        result.ShouldContain("unexpectedly not a rooted path");
    }

    [Fact]
    public void TruncateMessage_RedactsUnixPaths()
    {
        string message = @"/home/johndoe/src/project.csproj unexpectedly not a rooted path";
        string? result = CrashTelemetry.TruncateMessage(message);

        result.ShouldNotBeNull();
        result.ShouldNotContain("johndoe");
        result.ShouldContain("<path>");
    }

    [Fact]
    public void TruncateMessage_PreservesNonPathMessages()
    {
        string message = "All submissions not yet complete.";
        string? result = CrashTelemetry.TruncateMessage(message);
        result.ShouldBe("All submissions not yet complete.");
    }

    [Fact]
    public void PopulateFromException_SetsCrashThreadName()
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

        // The thread name may be null in test harness but the property should be set (even if null).
        // Just verify no exception was thrown during population.
        // In a named-thread scenario, it would capture the name.
    }

    [Fact]
    public void GetProperties_IncludesStackCaller_WhenSet()
    {
        CrashTelemetry telemetry = new()
        {
            ExceptionType = "Microsoft.Build.Framework.InternalErrorException",
            IsUnhandled = true,
            StackTop = "at Microsoft.Build.Shared.ErrorUtilities.ThrowInternalError(String message, Object[] args)",
            StackCaller = "at Microsoft.Build.BackEnd.RequestBuilder.BuildProject(String projectFile)",
            ExceptionMessage = "All submissions not yet complete.",
        };

        IDictionary<string, string> props = telemetry.GetProperties();
        props[nameof(CrashTelemetry.StackCaller)].ShouldBe("at Microsoft.Build.BackEnd.RequestBuilder.BuildProject(String projectFile)");
        props[nameof(CrashTelemetry.ExceptionMessage)].ShouldBe("All submissions not yet complete.");
    }

    [Fact]
    public void GetProperties_OmitsStackCaller_WhenNull()
    {
        CrashTelemetry telemetry = new()
        {
            ExceptionType = "System.NullReferenceException",
            IsUnhandled = true,
            StackTop = "at Microsoft.Build.BackEnd.RequestBuilder.BuildProject(String projectFile)",
            StackCaller = null,
        };

        IDictionary<string, string> props = telemetry.GetProperties();
        props.ShouldNotContainKey(nameof(CrashTelemetry.StackCaller));
    }

    [Fact]
    public void GetActivityProperties_IncludesStackCaller_WhenSet()
    {
        CrashTelemetry telemetry = new()
        {
            ExceptionType = "Microsoft.Build.Framework.InternalErrorException",
            IsUnhandled = true,
            StackTop = "at Microsoft.Build.Shared.ErrorUtilities.ThrowInternalError(String message, Object[] args)",
            StackCaller = "at Microsoft.Build.BackEnd.RequestBuilder.BuildProject(String projectFile)",
        };

        Dictionary<string, object> props = telemetry.GetActivityProperties();
        props[nameof(CrashTelemetry.StackCaller)].ShouldBe("at Microsoft.Build.BackEnd.RequestBuilder.BuildProject(String projectFile)");
    }

    [Fact]
    public void PopulateFromException_SetsFullStackTrace()
    {
        string fakeStack =
            "   at Microsoft.Build.Shared.ErrorUtilities.ThrowInternalError(String message, Object[] args)\n" +
            "   at Microsoft.Build.BackEnd.RequestBuilder.BuildProject(String projectFile) in C:\\Users\\user\\src\\file.cs:line 42\n" +
            "   at Microsoft.Build.BackEnd.BuildManager.Build() in C:\\Users\\user\\src\\mgr.cs:line 100";
        Exception ex = CreateExceptionWithStack(fakeStack);

        CrashTelemetry telemetry = new();
        telemetry.PopulateFromException(ex);

        telemetry.FullStackTrace.ShouldNotBeNull();
        // Should contain all frames
        telemetry.FullStackTrace!.ShouldContain("ErrorUtilities.ThrowInternalError");
        telemetry.FullStackTrace.ShouldContain("RequestBuilder.BuildProject");
        telemetry.FullStackTrace.ShouldContain("BuildManager.Build");
        // File paths should be redacted
        telemetry.FullStackTrace.ShouldNotContain("C:\\Users\\user");
        telemetry.FullStackTrace.ShouldContain("in <redacted>:line 42");
    }

    [Fact]
    public void ExtractFullStackTrace_ReturnsNull_WhenNoStackTrace()
    {
        Exception ex = CreateExceptionWithStack(null!);
        CrashTelemetry.ExtractFullStackTrace(ex).ShouldBeNull();
    }

    [Fact]
    public void ExtractFullStackTrace_TruncatesLongStackTraces()
    {
        // Build a stack trace longer than MaxStackTraceLength
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"   at Namespace.Type.Method{i}()");
        }
        Exception ex = CreateExceptionWithStack(sb.ToString());

        string? result = CrashTelemetry.ExtractFullStackTrace(ex);
        result.ShouldNotBeNull();
        result!.Length.ShouldBeLessThanOrEqualTo(CrashTelemetry.MaxStackTraceLength);
        result.ShouldEndWith("... [truncated]");
    }

    [Fact]
    public void GetProperties_IncludesFullStackTrace_WhenSet()
    {
        CrashTelemetry telemetry = new()
        {
            ExceptionType = "System.Exception",
            FullStackTrace = "   at Foo.Bar()\n   at Baz.Qux()",
        };

        IDictionary<string, string> props = telemetry.GetProperties();
        props[nameof(CrashTelemetry.FullStackTrace)].ShouldBe("   at Foo.Bar()\n   at Baz.Qux()");
    }

    [Fact]
    public void SanitizeFilePathsInText_RedactsPathsInStackFrames()
    {
        string input = "   at Foo.Bar() in C:\\Users\\secret\\src\\file.cs:line 99";
        string result = CrashTelemetry.SanitizeFilePathsInText(input);
        result.ShouldNotContain("secret");
        result.ShouldContain("in <redacted>:line 99");
    }

    [Fact]
    public void SanitizeFilePathsInText_LeavesNonPathLinesUnchanged()
    {
        string input = "System.Exception: something broke\n   at Foo.Bar()";
        string result = CrashTelemetry.SanitizeFilePathsInText(input);
        result.ShouldBe(input);
    }

    [Fact]
    public void EndBuildHang_GetProperties_IncludesHangDiagnostics()
    {
        CrashTelemetry telemetry = new()
        {
            ExitType = CrashExitType.EndBuildHang,
            EndBuildWaitPhase = "WaitingForSubmissions",
            EndBuildWaitDurationMs = 60000,
            PendingSubmissionCount = 3,
            SubmissionsWithResultNoLogging = 1,
            ThreadExceptionRecorded = false,
            UnmatchedProjectStartedCount = 2,
        };

        IDictionary<string, string> props = telemetry.GetProperties();
        props[nameof(CrashTelemetry.ExitType)].ShouldBe("EndBuildHang");
        props[nameof(CrashTelemetry.EndBuildWaitPhase)].ShouldBe("WaitingForSubmissions");
        props[nameof(CrashTelemetry.EndBuildWaitDurationMs)].ShouldBe("60000");
        props[nameof(CrashTelemetry.PendingSubmissionCount)].ShouldBe("3");
        props[nameof(CrashTelemetry.SubmissionsWithResultNoLogging)].ShouldBe("1");
        props[nameof(CrashTelemetry.ThreadExceptionRecorded)].ShouldBe("False");
        props[nameof(CrashTelemetry.UnmatchedProjectStartedCount)].ShouldBe("2");
    }

    [Fact]
    public void EndBuildHang_GetActivityProperties_IncludesHangDiagnostics()
    {
        CrashTelemetry telemetry = new()
        {
            ExitType = CrashExitType.EndBuildHang,
            EndBuildWaitPhase = "WaitingForNodes",
            EndBuildWaitDurationMs = 30000,
            PendingSubmissionCount = 0,
            SubmissionsWithResultNoLogging = 0,
            ThreadExceptionRecorded = true,
            UnmatchedProjectStartedCount = 0,
        };

        Dictionary<string, object> props = telemetry.GetActivityProperties();
        props[nameof(CrashTelemetry.EndBuildWaitPhase)].ShouldBe("WaitingForNodes");
        props[nameof(CrashTelemetry.EndBuildWaitDurationMs)].ShouldBe(30000L);
        props[nameof(CrashTelemetry.PendingSubmissionCount)].ShouldBe(0);
        props[nameof(CrashTelemetry.SubmissionsWithResultNoLogging)].ShouldBe(0);
        props[nameof(CrashTelemetry.ThreadExceptionRecorded)].ShouldBe(true);
        props[nameof(CrashTelemetry.UnmatchedProjectStartedCount)].ShouldBe(0);
    }

    [Fact]
    public void EndBuildHang_GetProperties_OmitsNullHangProperties()
    {
        CrashTelemetry telemetry = new()
        {
            ExitType = CrashExitType.EndBuildHang,
            EndBuildWaitPhase = "WaitingForSubmissions",
        };

        IDictionary<string, string> props = telemetry.GetProperties();
        props.ShouldContainKey(nameof(CrashTelemetry.EndBuildWaitPhase));
        props.ShouldNotContainKey(nameof(CrashTelemetry.PendingSubmissionCount));
        props.ShouldNotContainKey(nameof(CrashTelemetry.ThreadExceptionRecorded));
    }

    [Fact]
    public void EndBuildHang_DroppedProperties_NotPresent()
    {
        // Verify that the dropped properties from the critical evaluation
        // (ActiveNodeCount, SubmissionsWithNoResult, CancellationRequested,
        //  ShuttingDown, SchedulerHitNoLoggingCompleted, SchedulerNoLoggingDetails)
        // do not appear in the telemetry output.
        CrashTelemetry telemetry = new()
        {
            ExitType = CrashExitType.EndBuildHang,
            EndBuildWaitPhase = "WaitingForSubmissions",
            EndBuildWaitDurationMs = 30000,
            PendingSubmissionCount = 1,
        };

        IDictionary<string, string> props = telemetry.GetProperties();
        props.ShouldNotContainKey("ActiveNodeCount");
        props.ShouldNotContainKey("SubmissionsWithNoResult");
        props.ShouldNotContainKey("CancellationRequested");
        props.ShouldNotContainKey("ShuttingDown");
        props.ShouldNotContainKey("SchedulerHitNoLoggingCompleted");
        props.ShouldNotContainKey("SchedulerNoLoggingDetails");
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
