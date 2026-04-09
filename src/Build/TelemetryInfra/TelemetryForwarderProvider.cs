// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;

namespace Microsoft.Build.TelemetryInfra;

/// <summary>
/// A build component that indicates whether telemetry collection is enabled.
/// Telemetry data accumulation has moved to <see cref="BuildRequestEngine"/> (per-engine ownership),
/// eliminating singleton contention and per-request dictionary allocations.
/// </summary>
internal class TelemetryForwarderProvider : IBuildComponent
{
    private ITelemetryForwarder? _instance;

    public ITelemetryForwarder Instance => _instance ?? new NullTelemetryForwarder();

    internal static IBuildComponent CreateComponent(BuildComponentType type)
    {
        ErrorUtilities.VerifyThrow(type == BuildComponentType.TelemetryForwarder, "Cannot create components of type {0}", type);
        return new TelemetryForwarderProvider();
    }

    public void InitializeComponent(IBuildComponentHost host)
    {
        ErrorUtilities.VerifyThrow(host != null, "BuildComponentHost was null");

        if (_instance == null)
        {
            if (host!.BuildParameters.IsTelemetryEnabled)
            {
                _instance = new TelemetryForwarder();
            }
            else
            {
                _instance = new NullTelemetryForwarder();
            }
        }
    }

    public void ShutdownComponent()
    {
        /* Too late here for any communication to the main node or for logging anything. Just cleanup. */
        _instance = null;
    }

    /// <summary>
    /// Telemetry forwarder that reports whether telemetry collection is active.
    /// Data accumulation is handled by <see cref="BuildRequestEngine"/> per-engine.
    /// </summary>
    public class TelemetryForwarder : ITelemetryForwarder
    {
        public bool IsTelemetryCollected => true;

        public void FinalizeProcessing(LoggingContext loggingContext)
        {
            // No-op: telemetry data is now sent by BuildRequestEngine.SendTelemetryData.
        }
    }

    public class NullTelemetryForwarder : ITelemetryForwarder
    {
        public bool IsTelemetryCollected => false;

        public void FinalizeProcessing(LoggingContext loggingContext) { }
    }
}
