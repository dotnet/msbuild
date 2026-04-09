// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.TelemetryInfra;

/// <summary>
/// A build component that indicates whether telemetry collection is enabled
/// and provides a finalization hook.
/// </summary>
internal interface ITelemetryForwarder
{
    bool IsTelemetryCollected { get; }

    /// <summary>
    /// Sends accumulated telemetry and resets internal state.
    /// </summary>
    void FinalizeProcessing(LoggingContext loggingContext);
}
