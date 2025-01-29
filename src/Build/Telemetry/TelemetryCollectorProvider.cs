// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Telemetry;

/// <summary>
/// A build component responsible for accumulating telemetry data from worker node and then sending it to main node
/// at the end of the build.
/// </summary>
internal class TelemetryCollectorProvider : IBuildComponent
{
    private ITelemetryCollector? _instance;

    public ITelemetryCollector Instance => _instance ?? new NullTelemetryCollector();

    internal static IBuildComponent CreateComponent(BuildComponentType type)
    {
        ErrorUtilities.VerifyThrow(type == BuildComponentType.TelemetryCollector, "Cannot create components of type {0}", type);
        return new TelemetryCollectorProvider();
    }

    public void InitializeComponent(IBuildComponentHost host)
    {
        ErrorUtilities.VerifyThrow(host != null, "BuildComponentHost was null");

        if (_instance == null)
        {
            if (host!.BuildParameters.IsTelemetryEnabled)
            {
                _instance = new TelemetryCollector();
            }
            else
            {
                _instance = new NullTelemetryCollector();
            }
        }
    }

    public void ShutdownComponent()
    {
        /* Too late here for any communication to the main node or for logging anything. Just cleanup. */
        _instance = null;
    }

    public class TelemetryCollector : ITelemetryCollector
    {
        private readonly WorkerNodeTelemetryData _workerNodeTelemetryData = new();

        // in future, this might be per event type
        public bool IsTelemetryCollected => true;

        public void AddTask(string name, TimeSpan cumulativeExectionTime, short executionsCount, long totalMemoryConsumed, bool isCustom, bool isFromNugetCache)
        {
            name = GetName(name, isCustom, false, isFromNugetCache);
            _workerNodeTelemetryData.AddTask(name, cumulativeExectionTime, executionsCount, totalMemoryConsumed);
        }

        public void AddTarget(string name, bool wasExecuted, bool isCustom, bool isMetaproj, bool isFromNugetCache)
        {
            name = GetName(name, isCustom, isMetaproj, isFromNugetCache);
            _workerNodeTelemetryData.AddTarget(name, wasExecuted);
        }

        private static string GetName(string name, bool isCustom, bool isMetaproj, bool isFromNugetCache)
        {
            if (isMetaproj)
            {
                name = WorkerNodeTelemetryData.MetaProjPrefix + name;
            }

            if (isCustom)
            {
                name = WorkerNodeTelemetryData.CustomPrefix + name;
            }

            if (isFromNugetCache)
            {
                name = WorkerNodeTelemetryData.FromNugetPrefix + name;
            }

            return name;
        }

        public void FinalizeProcessing(LoggingContext loggingContext)
        {
            WorkerNodeTelemetryEventArgs telemetryArgs = new(_workerNodeTelemetryData)
                { BuildEventContext = loggingContext.BuildEventContext };
            loggingContext.LogBuildEvent(telemetryArgs);
        }
    }

    public class NullTelemetryCollector : ITelemetryCollector
    {
        public bool IsTelemetryCollected => false;

        public void AddTask(string name, TimeSpan cumulativeExectionTime, short executionsCount, long totalMemoryConsumed, bool isCustom, bool isFromNugetCache) { }
        public void AddTarget(string name, bool wasExecuted, bool isCustom, bool isMetaproj, bool isFromNugetCache) { }

        public void FinalizeProcessing(LoggingContext loggingContext) { }
    }
}
