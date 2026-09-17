// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Part of BuildTelemetry which is collected on client and needs to be sent to server,
/// so server can log BuildTelemetry once it is finished.
/// </summary>
internal sealed class PartialBuildTelemetry : ITranslatable
{
    private DateTime _startedAt = default;
    private string? _initialServerState = default;
    private string? _serverFallbackReason = default;
    private string? _serverEnableReason = default;

    public PartialBuildTelemetry(DateTime startedAt, string? initialServerState, string? serverFallbackReason, string? serverEnableReason)
    {
        _startedAt = startedAt;
        _initialServerState = initialServerState;
        _serverFallbackReason = serverFallbackReason;
        _serverEnableReason = serverEnableReason;
    }

    /// <summary>
    /// Constructor for deserialization
    /// </summary>
    private PartialBuildTelemetry()
    {
    }

    public DateTime? StartedAt => _startedAt;

    public string? InitialServerState => _initialServerState;

    public string? ServerFallbackReason => _serverFallbackReason;

    public string? ServerEnableReason => _serverEnableReason;

    public void Translate(ITranslator translator)
    {
        translator.Translate(ref _startedAt);
        translator.Translate(ref _initialServerState);
        translator.Translate(ref _serverFallbackReason);
        translator.Translate(ref _serverEnableReason);
    }

    internal static PartialBuildTelemetry FactoryForDeserialization(ITranslator translator)
    {
        PartialBuildTelemetry partialTelemetryData = new();
        partialTelemetryData.Translate(translator);
        return partialTelemetryData;
    }
}
