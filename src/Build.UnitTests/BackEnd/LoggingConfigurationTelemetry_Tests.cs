// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using System;
using System.Globalization;
using System.Linq;
using Microsoft.Build.Framework.Telemetry;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Telemetry;

public class LoggingConfigurationTelemetry_Tests
{
    [Fact]
    public void LoggingConfigurationTelemetryIsThere()
    {
        KnownTelemetry.LoggingConfigurationTelemetry.ShouldNotBeNull();
    }

    [Fact]
    public void BuildTelemetryConstructedHasNoProperties()
    {
        LoggingConfigurationTelemetry telemetry = new();

        telemetry.EventName.ShouldBe("loggingConfiguration");
        telemetry.TerminalLogger.ShouldBe(false);
        telemetry.TerminalLoggerUserIntent.ShouldBeNull();
        telemetry.TerminalLoggerUserIntentSource.ShouldBeNull();
        telemetry.TerminalLoggerDefault.ShouldBeNull();
        telemetry.TerminalLoggerDefaultSource.ShouldBeNull();
        telemetry.ConsoleLogger.ShouldBe(false);
        telemetry.ConsoleLoggerType.ShouldBeNull();
        telemetry.ConsoleLoggerVerbosity.ShouldBeNull();
        telemetry.FileLogger.ShouldBe(false);
        telemetry.FileLoggerVerbosity.ShouldBeNull();
        telemetry.FileLoggersCount.ShouldBe(0);
        telemetry.FileLoggerVerbosity.ShouldBeNull();
        telemetry.BinaryLogger.ShouldBe(false);
        telemetry.BinaryLoggerUsedDefaultName.ShouldBe(false);

        telemetry.GetProperties().Where(kv => kv.Value != bool.FalseString).ShouldBeEmpty();
    }

    [Fact]
    public void BuildTelemetryCreateProperProperties()
    {
        LoggingConfigurationTelemetry telemetry = new()
        {
            TerminalLogger = true,
            TerminalLoggerUserIntent = "on",
            TerminalLoggerUserIntentSource = "arg",
            TerminalLoggerDefault = "auto",
            TerminalLoggerDefaultSource = "sdk",
            ConsoleLogger = true,
            ConsoleLoggerType = "serial",
            ConsoleLoggerVerbosity = "minimal",
            FileLogger = true,
            FileLoggerType = "serial",
            FileLoggersCount = 2,
            FileLoggerVerbosity = "normal",
            BinaryLogger = true,
            BinaryLoggerUsedDefaultName = true
        };

        var properties = telemetry.GetProperties();
        properties["TerminalLogger"].ShouldBe(bool.TrueString);
        properties["TerminalLoggerUserIntent"].ShouldBe("on");
        properties["TerminalLoggerUserIntentSource"].ShouldBe("arg");
        properties["TerminalLoggerDefault"].ShouldBe("auto");
        properties["TerminalLoggerDefaultSource"].ShouldBe("sdk");
        properties["ConsoleLogger"].ShouldBe(bool.TrueString);
        properties["ConsoleLoggerType"].ShouldBe("serial");
        properties["ConsoleLoggerVerbosity"].ShouldBe("minimal");
        properties["FileLogger"].ShouldBe(bool.TrueString);
        properties["FileLoggerType"].ShouldBe("serial");
        properties["FileLoggersCount"].ShouldBe("2");
        properties["FileLoggerVerbosity"].ShouldBe("normal");
        properties["BinaryLogger"].ShouldBe(bool.TrueString);
        properties["BinaryLoggerUsedDefaultName"].ShouldBe(bool.TrueString);
    }
}
