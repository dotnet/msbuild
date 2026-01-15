// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd;

public class BuildTelemetryErrorCategorization_Tests
{
    [Theory]
    [InlineData("CS0103", null, "Compiler")]
    [InlineData("CS1002", "CS", "Compiler")]
    [InlineData("VBC30451", "VBC", "Compiler")]
    [InlineData("FS0039", null, "Compiler")]
    [InlineData("MSB4018", null, "MSBuildEngine")]
    [InlineData("MSB4236", null, "SDKResolvers")]
    [InlineData("MSB3026", null, "Tasks")]
    [InlineData("NETSDK1045", null, "NETSDK")]
    [InlineData("NU1101", null, "NuGet")]
    [InlineData("BC0001", null, "BuildCheck")]
    [InlineData("CUSTOM001", null, "Other")]
    [InlineData(null, null, "Other")]
    [InlineData("", null, "Other")]
    public void ErrorCategorizationWorksCorrectly(string errorCode, string subcategory, string expectedCategory)
    {
        // Create a LoggingService
        var loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        loggingService.OnlyLogCriticalEvents = false;

        try
        {
            // Log an error with the specified code
            var errorEvent = new BuildErrorEventArgs(
                subcategory,
                errorCode,
                "file.cs",
                1,
                1,
                0,
                0,
                "Test error message",
                "helpKeyword",
                "sender");

            loggingService.LogBuildEvent(errorEvent);

            // Populate telemetry
            var buildTelemetry = new BuildTelemetry();
            loggingService.PopulateBuildTelemetryWithErrors(buildTelemetry);

            // Verify the category is set correctly
            buildTelemetry.FailureCategory.ShouldBe(expectedCategory);

            // Verify the appropriate count is incremented
            switch (expectedCategory)
            {
                case "Compiler":
                    buildTelemetry.ErrorCounts.Compiler.ShouldBe(1);
                    break;
                case "MSBuildEngine":
                    buildTelemetry.ErrorCounts.MsBuildEngine.ShouldBe(1);
                    break;
                case "Tasks":
                    buildTelemetry.ErrorCounts.Task.ShouldBe(1);
                    break;
                case "SDKResolvers":
                    buildTelemetry.ErrorCounts.SdkResolvers.ShouldBe(1);
                    break;
                case "NETSDK":
                    buildTelemetry.ErrorCounts.NetSdk.ShouldBe(1);
                    break;
                case "NuGet":
                    buildTelemetry.ErrorCounts.NuGet.ShouldBe(1);
                    break;
                case "BuildCheck":
                    buildTelemetry.ErrorCounts.BuildCheck.ShouldBe(1);
                    break;
                case "Other":
                    buildTelemetry.ErrorCounts.Other.ShouldBe(1);
                    break;
            }
        }
        finally
        {
            loggingService.ShutdownComponent();
        }
    }

    [Fact]
    public void MultipleErrorsAreCountedByCategory()
    {
        var loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        loggingService.OnlyLogCriticalEvents = false;

        try
        {
            // Log multiple errors of different categories
            var errors = new[]
            {
                new BuildErrorEventArgs(null, "CS0103", "file.cs", 1, 1, 0, 0, "Error 1", null, "sender"),
                new BuildErrorEventArgs(null, "CS1002", "file.cs", 2, 1, 0, 0, "Error 2", null, "sender"),
                new BuildErrorEventArgs(null, "MSB4018", "file.proj", 10, 5, 0, 0, "Error 3", null, "sender"),
                new BuildErrorEventArgs(null, "MSB3026", "file.proj", 15, 3, 0, 0, "Error 4", null, "sender"),
                new BuildErrorEventArgs(null, "NU1101", "file.proj", 20, 1, 0, 0, "Error 5", null, "sender"),
                new BuildErrorEventArgs(null, "CUSTOM001", "file.txt", 1, 1, 0, 0, "Error 6", null, "sender"),
            };

            foreach (var error in errors)
            {
                loggingService.LogBuildEvent(error);
            }

            // Populate telemetry
            var buildTelemetry = new BuildTelemetry();
            loggingService.PopulateBuildTelemetryWithErrors(buildTelemetry);

            // Verify counts
            buildTelemetry.ErrorCounts.Compiler.ShouldBe(2);
            buildTelemetry.ErrorCounts.MsBuildEngine.ShouldBe(1);
            buildTelemetry.ErrorCounts.Task.ShouldBe(1);
            buildTelemetry.ErrorCounts.NuGet.ShouldBe(1);
            buildTelemetry.ErrorCounts.Other.ShouldBe(1);

            // Primary category should be Compiler (highest count)
            buildTelemetry.FailureCategory.ShouldBe("Compiler");
        }
        finally
        {
            loggingService.ShutdownComponent();
        }
    }

    [Fact]
    public void PrimaryCategoryIsSetToHighestErrorCount()
    {
        var loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        loggingService.OnlyLogCriticalEvents = false;

        try
        {
            // Log errors with Tasks having the highest count
            var errors = new[]
            {
                new BuildErrorEventArgs(null, "MSB3026", "file.proj", 1, 1, 0, 0, "Task Error 1", null, "sender"),
                new BuildErrorEventArgs(null, "MSB3027", "file.proj", 2, 1, 0, 0, "Task Error 2", null, "sender"),
                new BuildErrorEventArgs(null, "MSB3028", "file.proj", 3, 1, 0, 0, "Task Error 3", null, "sender"),
                new BuildErrorEventArgs(null, "CS0103", "file.cs", 4, 1, 0, 0, "Compiler Error", null, "sender"),
            };

            foreach (var error in errors)
            {
                loggingService.LogBuildEvent(error);
            }

            // Populate telemetry
            var buildTelemetry = new BuildTelemetry();
            loggingService.PopulateBuildTelemetryWithErrors(buildTelemetry);

            // Primary category should be Tasks (3 errors vs 1 compiler error)
            buildTelemetry.FailureCategory.ShouldBe("Tasks");
            buildTelemetry.ErrorCounts.Task.ShouldBe(3);
            buildTelemetry.ErrorCounts.Compiler.ShouldBe(1);
        }
        finally
        {
            loggingService.ShutdownComponent();
        }
    }

    [Fact]
    public void SubcategoryIsUsedForCompilerErrors()
    {
        var loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        loggingService.OnlyLogCriticalEvents = false;

        try
        {
            // Log an error with subcategory "CS" (common for C# compiler errors)
            var errorEvent = new BuildErrorEventArgs(
                "CS",  // subcategory
                "CS0103",
                "file.cs",
                1,
                1,
                0,
                0,
                "The name 'foo' does not exist in the current context",
                "helpKeyword",
                "csc");

            loggingService.LogBuildEvent(errorEvent);

            // Populate telemetry
            var buildTelemetry = new BuildTelemetry();
            loggingService.PopulateBuildTelemetryWithErrors(buildTelemetry);

            // Should be categorized as Compiler based on subcategory
            buildTelemetry.FailureCategory.ShouldBe("Compiler");
            buildTelemetry.ErrorCounts.Compiler.ShouldBe(1);
        }
        finally
        {
            loggingService.ShutdownComponent();
        }
    }
}
