// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Telemetry;

internal interface ITelemetryCollector
{
    bool IsTelemetryCollected { get; }

    void AddTask(string name, TimeSpan cumulativeExectionTime, short executionsCount, long totalMemoryConsumed, bool isCustom,
        bool isFromNugetCache);

    // wasExecuted - means anytime, not necessarily from the last time target was added to telemetry
    void AddTarget(string name, bool wasExecuted, bool isCustom, bool isFromNugetCache);

    void FinalizeProcessing(LoggingContext loggingContext);
}

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

        // in future, this might ber per event type
        public bool IsTelemetryCollected => true;

        public void AddTask(string name, TimeSpan cumulativeExectionTime, short executionsCount, long totalMemoryConsumed, bool isCustom, bool isFromNugetCache)
        {
            name = GetName(name, isCustom, isFromNugetCache);
            _workerNodeTelemetryData.AddTask(name, cumulativeExectionTime, executionsCount, totalMemoryConsumed);
        }

        public void AddTarget(string name, bool wasExecuted, bool isCustom, bool isFromNugetCache)
        {
            name = GetName(name, isCustom, isFromNugetCache);
            _workerNodeTelemetryData.AddTarget(name, wasExecuted);
        }

        private static string GetName(string name, bool isCustom, bool isFromNugetCache)
        {
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
        public void AddTarget(string name, bool wasExecuted, bool isCustom, bool isFromNugetCache) { }

        public void FinalizeProcessing(LoggingContext loggingContext) { }
    }
}
