// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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

    public override IDictionary<string, string> GetProperties()
    {
        var properties = new Dictionary<string, string>();

        // populate property values
        properties["TerminalLogger"] = TerminalLogger.ToString(CultureInfo.InvariantCulture);

        if (TerminalLoggerUserIntent != null)
        {
            properties["TerminalLoggerUserIntent"] = TerminalLoggerUserIntent;
        }

        if (TerminalLoggerUserIntentSource != null)
        {
            properties["TerminalLoggerUserIntentSource"] = TerminalLoggerUserIntentSource;
        }

        if (TerminalLoggerDefault != null)
        {
            properties["TerminalLoggerDefault"] = TerminalLoggerDefault;
        }

        if (TerminalLoggerDefaultSource != null)
        {
            properties["TerminalLoggerDefaultSource"] = TerminalLoggerDefaultSource;
        }

        properties["ConsoleLogger"] = ConsoleLogger.ToString(CultureInfo.InvariantCulture);
        if (ConsoleLoggerType != null)
        {
            properties["ConsoleLoggerType"] = ConsoleLoggerType;
        }

        if (ConsoleLoggerVerbosity != null)
        {
            properties["ConsoleLoggerVerbosity"] = ConsoleLoggerVerbosity;
        }

        properties["FileLogger"] = FileLogger.ToString(CultureInfo.InvariantCulture);
        if (FileLoggerType != null)
        {
            properties["FileLoggerType"] = FileLoggerType;
            properties["FileLoggersCount"] = FileLoggersCount.ToString(CultureInfo.InvariantCulture);
        }

        if (FileLoggerVerbosity != null)
        {
            properties["FileLoggerVerbosity"] = FileLoggerVerbosity;
        }

        properties["BinaryLogger"] = BinaryLogger.ToString(CultureInfo.InvariantCulture);
        properties["BinaryLoggerUsedDefaultName"] = BinaryLoggerUsedDefaultName.ToString(CultureInfo.InvariantCulture);

        return properties;
    }
}
