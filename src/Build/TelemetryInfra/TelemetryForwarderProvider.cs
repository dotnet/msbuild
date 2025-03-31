// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
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

    public class TelemetryForwarder : ITelemetryForwarder
    {
        private readonly WorkerNodeTelemetryData _workerNodeTelemetryData = new();

        // in future, this might be per event type
        public bool IsTelemetryCollected => true;

        public void AddTask(string name, TimeSpan cumulativeExecutionTime, short executionsCount, long totalMemoryConsumed, bool isCustom, bool isFromNugetCache)
        {
            var key = GetKey(name, isCustom, false, isFromNugetCache);
            _workerNodeTelemetryData.AddTask(key, cumulativeExecutionTime, executionsCount, totalMemoryConsumed);
        }

        public void AddTarget(string name, bool wasExecuted, bool isCustom, bool isMetaproj, bool isFromNugetCache)
        {
            var key = GetKey(name, isCustom, isMetaproj, isFromNugetCache);
            _workerNodeTelemetryData.AddTarget(key, wasExecuted);
        }

        private static TaskOrTargetTelemetryKey GetKey(string name, bool isCustom, bool isMetaproj,
            bool isFromNugetCache)
            => new TaskOrTargetTelemetryKey(name, isCustom, isFromNugetCache, isMetaproj);

        public void FinalizeProcessing(LoggingContext loggingContext)
        {
            WorkerNodeTelemetryEventArgs telemetryArgs = new(_workerNodeTelemetryData)
                { BuildEventContext = loggingContext.BuildEventContext };
            loggingContext.LogBuildEvent(telemetryArgs);
        }
    }

    public class NullTelemetryForwarder : ITelemetryForwarder
    {
        public bool IsTelemetryCollected => false;

        public void AddTask(string name, TimeSpan cumulativeExecutionTime, short executionsCount, long totalMemoryConsumed, bool isCustom, bool isFromNugetCache) { }
        public void AddTarget(string name, bool wasExecuted, bool isCustom, bool isMetaproj, bool isFromNugetCache) { }

        public void FinalizeProcessing(LoggingContext loggingContext) { }
    }
}
