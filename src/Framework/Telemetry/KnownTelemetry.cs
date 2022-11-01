// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
