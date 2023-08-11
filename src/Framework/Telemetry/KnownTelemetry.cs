// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Static class to help access and modify known telemetries.
/// </summary>
internal static class KnownTelemetry
{
    /// <summary>
    /// Partial Telemetry for build.
    /// This could be optionally initialized with some values from early in call stack, for example in Main method.
    /// After this instance is acquired by a particular build, this is set to null.
    /// Null means there are no prior collected build telemetry data, new clean instance shall be created for particular build.
    /// </summary>
    public static BuildTelemetry? PartialBuildTelemetry { get; set; }

    /// <summary>
    /// Describes how logging was configured.
    /// </summary>
    public static LoggingConfigurationTelemetry LoggingConfigurationTelemetry { get; } = new LoggingConfigurationTelemetry();
}
