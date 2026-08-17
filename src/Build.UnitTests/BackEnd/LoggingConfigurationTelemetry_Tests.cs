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

        telemetry.UpdateEventProperties();
        telemetry.Properties.Where(kv => kv.Value != bool.FalseString).ShouldBeEmpty();
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

        telemetry.UpdateEventProperties();

        telemetry.Properties["TerminalLogger"].ShouldBe(bool.TrueString);
        telemetry.Properties["TerminalLoggerUserIntent"].ShouldBe("on");
        telemetry.Properties["TerminalLoggerUserIntentSource"].ShouldBe("arg");
        telemetry.Properties["TerminalLoggerDefault"].ShouldBe("auto");
        telemetry.Properties["TerminalLoggerDefaultSource"].ShouldBe("sdk");
        telemetry.Properties["ConsoleLogger"].ShouldBe(bool.TrueString);
        telemetry.Properties["ConsoleLoggerType"].ShouldBe("serial");
        telemetry.Properties["ConsoleLoggerVerbosity"].ShouldBe("minimal");
        telemetry.Properties["FileLogger"].ShouldBe(bool.TrueString);
        telemetry.Properties["FileLoggerType"].ShouldBe("serial");
        telemetry.Properties["FileLoggersCount"].ShouldBe("2");
        telemetry.Properties["FileLoggerVerbosity"].ShouldBe("normal");
        telemetry.Properties["BinaryLogger"].ShouldBe(bool.TrueString);
        telemetry.Properties["BinaryLoggerUsedDefaultName"].ShouldBe(bool.TrueString);
    }
}
