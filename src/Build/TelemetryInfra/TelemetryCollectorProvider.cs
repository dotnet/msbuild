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
/// Build component that creates per-BuildRequestEngine <see cref="ITelemetryCollector"/> instances.
/// Registered as a singleton, but holds no mutable state — each BuildRequestEngine gets its own
/// collector via <see cref="CreateCollector"/>.
/// </summary>
internal class TelemetryCollectorProvider : IBuildComponent
{
    private bool _telemetryEnabled;

    /// <summary>
    /// Creates a new <see cref="ITelemetryCollector"/> scoped to one <see cref="BuildRequestEngine"/>'s build lifetime.
    /// Returns a no-op collector when telemetry is disabled.
    /// </summary>
    internal ITelemetryCollector CreateCollector()
        => _telemetryEnabled ? new TelemetryCollector() : NullTelemetryCollector.Instance;

    internal static IBuildComponent CreateComponent(BuildComponentType type)
    {
        ErrorUtilities.VerifyThrow(type == BuildComponentType.TelemetryCollector, "Cannot create components of type {0}", type);
        return new TelemetryCollectorProvider();
    }

    public void InitializeComponent(IBuildComponentHost host)
    {
        ErrorUtilities.VerifyThrow(host != null, "BuildComponentHost was null");
        _telemetryEnabled = host!.BuildParameters.IsTelemetryEnabled;
    }

    public void ShutdownComponent()
    {
    }

    /// <summary>
    /// Collects task/target telemetry for one BuildRequestEngine. Not thread-safe —
    /// only one RequestBuilder is active at a time per BuildRequestEngine.
    /// </summary>
    internal class TelemetryCollector : ITelemetryCollector
    {
        private WorkerNodeTelemetryData _data = new();

        public bool IsTelemetryCollected => true;

        public void AddTarget(TaskOrTargetTelemetryKey key, bool wasExecuted, TargetSkipReason skipReason = TargetSkipReason.None)
        {
            _data.AddTarget(key, wasExecuted, skipReason);
        }

        public void AddTask(TaskOrTargetTelemetryKey key, TimeSpan cumulativeExecutionTime, int executionsCount, long totalMemoryConsumed, string? taskFactoryName, string? taskHostRuntime)
        {
            _data.AddTask(key, cumulativeExecutionTime, executionsCount, totalMemoryConsumed, taskFactoryName, taskHostRuntime);
        }

        public void FinalizeProcessing(LoggingContext loggingContext)
        {
            if (_data.IsEmpty)
            {
                return;
            }

            WorkerNodeTelemetryData snapshot = _data;
            _data = new();

            WorkerNodeTelemetryEventArgs telemetryArgs = new(snapshot)
            { BuildEventContext = loggingContext.BuildEventContext };
            loggingContext.LogBuildEvent(telemetryArgs);
        }
    }

    internal class NullTelemetryCollector : ITelemetryCollector
    {
        internal static readonly NullTelemetryCollector Instance = new();

        public bool IsTelemetryCollected => false;

        public void AddTarget(TaskOrTargetTelemetryKey key, bool wasExecuted, TargetSkipReason skipReason = TargetSkipReason.None) { }

        public void AddTask(TaskOrTargetTelemetryKey key, TimeSpan cumulativeExecutionTime, int executionsCount, long totalMemoryConsumed, string? taskFactoryName, string? taskHostRuntime) { }

        public void FinalizeProcessing(LoggingContext loggingContext) { }
    }
}
