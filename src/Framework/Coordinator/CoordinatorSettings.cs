// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.BackEnd;

#if NET
using Microsoft.Build.Utilities;
#endif

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
    public const int MaxHeartbeatIntervalMs = 300_000;

    private static string DefaultPipeName => $"{PipeNameBase}-{Environment.UserName}";

    private int? _heartbeatIntervalMs;
    private int? _missedHeartbeatsThreshold;
    private int? _connectionTimeoutMs;
    private int? _shutdownTimeoutMs;
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

    public int HeartbeatIntervalMs
    {
        get => _heartbeatIntervalMs ??= DefaultHeartbeatIntervalMs;
        init => _heartbeatIntervalMs = value > 0
            ? Math.Min(value, MaxHeartbeatIntervalMs)
            : DefaultHeartbeatIntervalMs;
    }

    public int MissedHeartbeatsThreshold
    {
        get => _missedHeartbeatsThreshold ??= DefaultMissedHeartbeatsThreshold;
        init => _missedHeartbeatsThreshold = value > 0 ? value : DefaultMissedHeartbeatsThreshold;
    }

    public int TotalNodeBudget
    {
        get => _totalNodeBudget ??= Environment.ProcessorCount;
        init => _totalNodeBudget = value <= 0 ? Environment.ProcessorCount : value;
    }

    public int ShutdownTimeoutMs
    {
        get => _shutdownTimeoutMs ??= DefaultShutdownTimeoutMs;
        init => _shutdownTimeoutMs = value >= 0 ? value : DefaultShutdownTimeoutMs;
    }

    public int ConnectionTimeoutMs
    {
        get => _connectionTimeoutMs ??= DefaultConnectionTimeoutMs;
        init => _connectionTimeoutMs = value > 0 ? value : DefaultConnectionTimeoutMs;
    }

    public int ProcessId
    {
        get => _processId ??= EnvironmentUtilities.CurrentProcessId;
        init => _processId = value > 0 ? value : EnvironmentUtilities.CurrentProcessId;
    }

    /// <summary>
    ///  Computes the heartbeat timeout in milliseconds using long arithmetic to avoid overflow.
    /// </summary>
    public long HeartbeatTimeoutMs => (long)HeartbeatIntervalMs * MissedHeartbeatsThreshold;

    /// <summary>
    ///  The named mutex used by the coordinator server to ensure single-instance execution.
    /// </summary>
    public string ServerMutexName => GetMutexName("server");

    /// <summary>
    ///  The named mutex used by clients to serialize coordinator launch attempts.
    /// </summary>
    public string LaunchMutexName => GetMutexName("launch");

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

    /// <summary>
    ///  Generates a platform-appropriate mutex name by combining the pipe name with a purpose suffix.
    /// </summary>
    private string GetMutexName(string purpose)
    {
        if (NativeMethods.IsWindows)
        {
            return $"Global\\{PipeName}-{purpose}";
        }

        // Named mutexes on Unix do not accept path-like names (for example '/tmp/...').
        // Hash the pipe name into a stable, compact identifier safe for the runtime.
        string prefix = $"msbuild-coordinator-{purpose}-";

#if NET
        int byteCount = Encoding.UTF8.GetByteCount(PipeName);
        using BufferScope<byte> pipeNameBytes = byteCount <= 256
            ? new(stackalloc byte[byteCount])
            : new(byteCount);

        Encoding.UTF8.GetBytes(PipeName, pipeNameBytes);

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(pipeNameBytes[..byteCount], hash);
#else
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(PipeName));
#endif

        // We're being a bit clever here by defining PrefixAndHash as a ref struct on modern .NET
        // since string.Create<TState> is defined with a 'where TState : allows ref struct' constraint.
        return string.Create(prefix.Length + (hash.Length * 2), new PrefixAndHash(prefix, hash), static (span, state) =>
        {
            var (prefix, hash) = state;

            prefix.CopyTo(span);
            span = span[prefix.Length..];

            for (int i = 0; i < hash.Length; i++)
            {
                byte b = hash[i];
                span[0] = HexDigitChar(b / 16);
                span[1] = HexDigitChar(b % 16);
                span = span[2..];
            }
        });

        static char HexDigitChar(int value)
            => (char)(value + (value < 10 ? '0' : 'a' - 10));
    }

#if NET
    private readonly ref struct PrefixAndHash
    {
        public readonly ReadOnlySpan<char> Prefix;
        public readonly ReadOnlySpan<byte> Hash;

        public PrefixAndHash(ReadOnlySpan<char> prefix, ReadOnlySpan<byte> hash)
        {
            Prefix = prefix;
            Hash = hash;
        }

        public void Deconstruct(out ReadOnlySpan<char> prefix, out ReadOnlySpan<byte> hash)
        {
            prefix = Prefix;
            hash = Hash;
        }
    }
#else
    private readonly struct PrefixAndHash(string prefix, byte[] hash)
    {
        public ReadOnlySpan<char> Prefix => prefix;

        public ReadOnlySpan<byte> Hash => hash;

        public void Deconstruct(out ReadOnlySpan<char> prefix, out ReadOnlySpan<byte> hash)
        {
            prefix = Prefix;
            hash = Hash;
        }
    }
#endif
}
