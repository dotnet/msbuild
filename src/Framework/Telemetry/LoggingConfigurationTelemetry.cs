// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Build.Framework.Telemetry;

internal class LoggingConfigurationTelemetry : TelemetryBase
{
    public override string EventName => "loggingConfiguration";

    /// <summary>
    /// True if terminal logger was used.
    /// </summary>
    public bool TerminalLogger { get; set; }

    /// <summary>
    /// What was user intent:
    ///   on | true -> user intent to enable logging
    ///   off | false -> user intent to disable logging
    ///   auto -> user intent to use logging if terminal allows it
    ///   null -> no user intent, using default
    /// </summary>
    public string? TerminalLoggerUserIntent { get; set; }

    /// <summary>
    /// How was user intent signaled:
    ///   arg -> from command line argument or rsp file
    ///   MSBUILDTERMINALLOGGER -> from environment variable
    ///   MSBUILDLIVELOGGER -> from environment variable
    ///   null -> no user intent
    /// </summary>
    public string? TerminalLoggerUserIntentSource { get; set; }

    /// <summary>
    /// The default behavior of terminal logger if user intent is not specified:
    ///   on | true -> enable logging
    ///   off | false -> disable logging
    ///   auto -> use logging if terminal allows it
    ///   null -> unspecified
    /// </summary>
    public string? TerminalLoggerDefault { get; set; }

    /// <summary>
    /// How was default behavior signaled:
    ///   sdk -> from SDK
    ///   DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER -> from environment variable
    ///   msbuild -> MSBuild hardcoded default
    ///   null -> unspecified
    /// </summary>
    public string? TerminalLoggerDefaultSource { get; set; }

    /// <summary>
    /// True if console logger was used.
    /// </summary>
    public bool ConsoleLogger { get; set; }

    /// <summary>
    /// Type of console logger: serial | parallel
    /// </summary>
    public string? ConsoleLoggerType { get; set; }

    /// <summary>
    /// Verbosity of console logger: quiet | minimal | normal | detailed | diagnostic
    /// </summary>
    public string? ConsoleLoggerVerbosity { get; set; }


    /// <summary>
    /// True if file logger was used.
    /// </summary>
    public bool FileLogger { get; set; }

    /// <summary>
    /// Type of file logger: serial | parallel
    /// </summary>
    public string? FileLoggerType { get; set; }

    /// <summary>
    /// Number of file loggers.
    /// </summary>
    public int FileLoggersCount { get; set; }

    /// <summary>
    /// Verbosity of file logger: quiet | minimal | normal | detailed | diagnostic
    /// </summary>
    public string? FileLoggerVerbosity { get; set; }

    /// <summary>
    /// True if binary logger was used.
    /// </summary>
    public bool BinaryLogger { get; set; }

    /// <summary>
    /// True if binary logger used default name i.e. no LogFile was specified.
    /// </summary>
    public bool BinaryLoggerUsedDefaultName { get; set; }

    public override void UpdateEventProperties()
    {
        Properties["TerminalLogger"] = TerminalLogger.ToString(CultureInfo.InvariantCulture);

        if (TerminalLoggerUserIntent != null)
        {
            Properties["TerminalLoggerUserIntent"] = TerminalLoggerUserIntent;
        }

        if (TerminalLoggerUserIntentSource != null)
        {
            Properties["TerminalLoggerUserIntentSource"] = TerminalLoggerUserIntentSource;
        }

        if (TerminalLoggerDefault != null)
        {
            Properties["TerminalLoggerDefault"] = TerminalLoggerDefault;
        }

        if (TerminalLoggerDefaultSource != null)
        {
            Properties["TerminalLoggerDefaultSource"] = TerminalLoggerDefaultSource;
        }

        Properties["ConsoleLogger"] = ConsoleLogger.ToString(CultureInfo.InvariantCulture);
        if (ConsoleLoggerType != null)
        {
            Properties["ConsoleLoggerType"] = ConsoleLoggerType;
        }

        if (ConsoleLoggerVerbosity != null)
        {
            Properties["ConsoleLoggerVerbosity"] = ConsoleLoggerVerbosity;
        }

        Properties["FileLogger"] = FileLogger.ToString(CultureInfo.InvariantCulture);
        if (FileLoggerType != null)
        {
            Properties["FileLoggerType"] = FileLoggerType;
            Properties["FileLoggersCount"] = FileLoggersCount.ToString(CultureInfo.InvariantCulture);
        }

        if (FileLoggerVerbosity != null)
        {
            Properties["FileLoggerVerbosity"] = FileLoggerVerbosity;
        }

        Properties["BinaryLogger"] = BinaryLogger.ToString(CultureInfo.InvariantCulture);
        Properties["BinaryLoggerUsedDefaultName"] = BinaryLoggerUsedDefaultName.ToString(CultureInfo.InvariantCulture);
    }
}
