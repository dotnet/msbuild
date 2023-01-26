// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Static class to help access and modify known telemetries.
/// </summary>
internal static class KnownTelemetry
{
    /// <summary>
    /// Telemetry for build.
    /// If null Telemetry is not supposed to be emitted.
    /// After telemetry is emitted, sender shall null it.
    /// </summary>
    public static BuildTelemetry? BuildTelemetry { get; set; }
}
