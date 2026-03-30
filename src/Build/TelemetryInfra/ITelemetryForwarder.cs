// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework.Telemetry;

namespace Microsoft.Build.TelemetryInfra;

/// <summary>
/// A build component responsible for accumulating telemetry data from worker node and then sending it to main node
/// at the end of the build.
/// </summary>
internal interface ITelemetryForwarder
{
    bool IsTelemetryCollected { get; }

    /// <summary>
    /// Merges a batch of telemetry data into this forwarder's accumulated state.
    /// </summary>
    void MergeWorkerData(IWorkerNodeTelemetryData data);

    /// <summary>
    /// Sends accumulated telemetry and resets internal state.
    /// </summary>
    void FinalizeProcessing(LoggingContext loggingContext);
}
