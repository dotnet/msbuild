// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Shared;

namespace Microsoft.Build.TelemetryInfra;

/// <summary>
/// A build component responsible for accumulating telemetry data from worker node and then sending it to main node
/// at the end of the build.
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
    /// Active telemetry forwarder that accumulates worker node telemetry.
    /// </summary>
    /// <remarks>
    /// Thread-safe: in /m /mt mode, multiple <see cref="BuildRequestEngine"/> instances share a single
    /// <see cref="TelemetryForwarderProvider"/> singleton, so <see cref="MergeWorkerData"/> and
    /// <see cref="FinalizeProcessing"/> may be called concurrently from different node threads.
    /// </remarks>
    public class TelemetryForwarder : ITelemetryForwarder
    {
        private WorkerNodeTelemetryData _workerNodeTelemetryData = new();
        private readonly LockType _lock = new();

        // in future, this might be per event type
        public bool IsTelemetryCollected => true;

        public void MergeWorkerData(IWorkerNodeTelemetryData data)
        {
            lock (_lock)
            {
                _workerNodeTelemetryData.Add(data);
            }
        }


        public void FinalizeProcessing(LoggingContext loggingContext)
        {
            WorkerNodeTelemetryData snapshot;

            lock (_lock)
            {
                // Nothing accumulated since the last call — skip sending.
                if (_workerNodeTelemetryData.IsEmpty)
                {
                    return;
                }

                snapshot = _workerNodeTelemetryData;
                _workerNodeTelemetryData = new();
            }

            WorkerNodeTelemetryEventArgs telemetryArgs = new(snapshot)
            { BuildEventContext = loggingContext.BuildEventContext };
            loggingContext.LogBuildEvent(telemetryArgs);
        }
    }

    public class NullTelemetryForwarder : ITelemetryForwarder
    {
        public bool IsTelemetryCollected => false;

        public void MergeWorkerData(IWorkerNodeTelemetryData data) { }

        public void FinalizeProcessing(LoggingContext loggingContext) { }
    }
}
