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
/// Build component that creates per-engine <see cref="ITelemetryForwarder"/> instances.
/// Registered as a singleton (<see cref="BuildComponentType.TelemetryForwarder"/>),
/// but holds no mutable state - each engine gets its own forwarder via <see cref="CreateForwarder"/>.
/// </summary>
internal class TelemetryForwarderProvider : IBuildComponent
{
    private bool _telemetryEnabled;

    /// <summary>
    /// Creates a new <see cref="ITelemetryForwarder"/> scoped to one engine's build lifetime.
    /// Returns a no-op forwarder when telemetry is disabled.
    /// </summary>
    internal ITelemetryForwarder CreateForwarder()
        => _telemetryEnabled ? new TelemetryForwarder() : NullTelemetryForwarder.Instance;

    internal static IBuildComponent CreateComponent(BuildComponentType type)
    {
        ErrorUtilities.VerifyThrow(type == BuildComponentType.TelemetryForwarder, "Cannot create components of type {0}", type);
        return new TelemetryForwarderProvider();
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
    /// Collects task/target telemetry for one engine. Not thread-safe - the engine's
    /// one-active-builder-at-a-time invariant guarantees single-threaded access.
    /// See <see href="https://github.com/dotnet/msbuild/issues/13531"/> for hardening
    /// the yield/reacquire protocol that this invariant depends on.
    /// </summary>
    internal class TelemetryForwarder : ITelemetryForwarder
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

    internal class NullTelemetryForwarder : ITelemetryForwarder
    {
        internal static readonly NullTelemetryForwarder Instance = new();

        public bool IsTelemetryCollected => false;

        public void AddTarget(TaskOrTargetTelemetryKey key, bool wasExecuted, TargetSkipReason skipReason = TargetSkipReason.None) { }

        public void AddTask(TaskOrTargetTelemetryKey key, TimeSpan cumulativeExecutionTime, int executionsCount, long totalMemoryConsumed, string? taskFactoryName, string? taskHostRuntime) { }

        public void FinalizeProcessing(LoggingContext loggingContext) { }
    }
}