// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Shared settings for coordinator client and server behavior.
/// </summary>
internal sealed record class CoordinatorSettings()
{
    public const string PipeNameBase = "msbuild-coordinator";
    public const int DefaultHeartbeatIntervalMs = 5_000;
    public const int DefaultMissedHeartbeatsThreshold = 3;
    public const int DefaultConnectionTimeoutMs = 5_000;
    public const int DefaultShutdownTimeoutMs = 60_000;

    private static string DefaultPipeName => $"{PipeNameBase}-{Environment.UserName}";

    private int? _totalNodeBudget;
    private int? _processId;

    /// <summary>
    ///  Singleton settings instance populated with default values.
    /// </summary>
    public static CoordinatorSettings Default { get; } = new();

    public string PipeName
    {
        get => field ??= NamedPipeUtil.GetPlatformSpecificPipeName(DefaultPipeName);
        init => field = NamedPipeUtil.GetPlatformSpecificPipeName(value);
    }

    public int HeartbeatIntervalMs { get; init; } = DefaultHeartbeatIntervalMs;

    public int MissedHeartbeatsThreshold { get; init; } = DefaultMissedHeartbeatsThreshold;

    public int TotalNodeBudget
    {
        get => _totalNodeBudget ??= Environment.ProcessorCount;
        init => _totalNodeBudget = value <= 0 ? Environment.ProcessorCount : value;
    }

    public int ShutdownTimeoutMs { get; init; } = DefaultShutdownTimeoutMs;

    public int ConnectionTimeoutMs { get; init; } = DefaultConnectionTimeoutMs;

    public int ProcessId
    {
        get => _processId ??= EnvironmentUtilities.CurrentProcessId;
        init => _processId = value > 0 ? value : EnvironmentUtilities.CurrentProcessId;
    }

    public static CoordinatorSettings FromEnvironment()
    {
        string? pipeNameOverride = Environment.GetEnvironmentVariable(Traits.CoordinatorPipeNameEnvVarName);
        string pipeName = !string.IsNullOrEmpty(pipeNameOverride)
            ? pipeNameOverride
            : DefaultPipeName;

        return Default with
        {
            PipeName = pipeName,
            HeartbeatIntervalMs = EnvironmentUtilities.GetValueAsInt32OrDefault(
                Traits.CoordinatorHeartbeatIntervalEnvVarName,
                DefaultHeartbeatIntervalMs),
            MissedHeartbeatsThreshold = DefaultMissedHeartbeatsThreshold,
            TotalNodeBudget = EnvironmentUtilities.GetValueAsInt32OrDefault(
                Traits.CoordinatorNodeBudgetEnvVarName,
                Environment.ProcessorCount),
            ShutdownTimeoutMs = EnvironmentUtilities.GetValueAsInt32OrDefault(
                Traits.CoordinatorShutdownTimeoutEnvVarName,
                DefaultShutdownTimeoutMs),
            ConnectionTimeoutMs = DefaultConnectionTimeoutMs,
            ProcessId = EnvironmentUtilities.CurrentProcessId,
        };
    }
}
