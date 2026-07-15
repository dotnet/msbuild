// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Microsoft.Build.BackEnd;

internal enum TaskHostDictionaryTransferKind : byte
{
    Full,
    Delta,
    Unchanged,
}

internal enum TaskHostTaskParameterTransferKind : byte
{
    Direct,
    CachedValue,
    CacheReference,
    UncachedValue,
}

internal sealed class TaskHostConfigurationCache
{
    public Dictionary<string, string>? BuildProcessEnvironment;

    public Dictionary<string, string>? GlobalProperties;

    public TaskHostTaskParameterCache TaskParameters { get; } = new();

    public void Reset()
    {
        BuildProcessEnvironment = null;
        GlobalProperties = null;
        TaskParameters.Reset();
    }
}

internal sealed class TaskHostTaskParameterCache
{
    private const int CompressionThreshold = 32 * 1024;
    private const int MaxCachedEntries = 4096;
    private const long MaxCachedBytes = 128L * 1024 * 1024;

    private readonly Dictionary<ulong, List<WriteEntry>> _writeEntries = [];
    // Every child entry originates from a parent CachedValue entry, so the parent's entry and
    // byte limits also bound this dictionary even though the child stores compressed payloads.
    private readonly Dictionary<int, ReadEntry> _readEntries = [];
    private int _nextId;
    private long _cachedBytes;

    public void Prepare(
        Dictionary<string, TaskParameter>? taskParameters,
        byte packetVersion,
        out TaskHostTaskParameterTransferKind transferKind,
        out int cacheId,
        out byte[]? payload,
        out bool payloadCompressed,
        out byte payloadVersion)
    {
        byte[] serialized = Serialize(taskParameters, packetVersion);
        ulong hash = GetHash(serialized);
        payloadVersion = packetVersion;

        if (_writeEntries.TryGetValue(hash, out List<WriteEntry>? candidates))
        {
            foreach (WriteEntry candidate in candidates)
            {
                if (serialized.AsSpan().SequenceEqual(candidate.Serialized))
                {
                    transferKind = TaskHostTaskParameterTransferKind.CacheReference;
                    cacheId = candidate.Id;
                    payload = null;
                    payloadCompressed = false;
                    payloadVersion = 0;
                    return;
                }
            }
        }

        byte[] wirePayload = Compress(serialized, out payloadCompressed);
        if (_nextId < MaxCachedEntries &&
            _cachedBytes + serialized.Length + wirePayload.Length <= MaxCachedBytes)
        {
            cacheId = ++_nextId;
            transferKind = TaskHostTaskParameterTransferKind.CachedValue;
            payload = wirePayload;
            candidates ??= [];
            candidates.Add(new WriteEntry(cacheId, serialized));
            _writeEntries[hash] = candidates;
            _cachedBytes += serialized.Length;
            return;
        }

        transferKind = TaskHostTaskParameterTransferKind.UncachedValue;
        cacheId = 0;
        payload = wirePayload;
    }

    public Dictionary<string, TaskParameter>? Apply(
        TaskHostTaskParameterTransferKind transferKind,
        int cacheId,
        byte[]? payload,
        bool payloadCompressed,
        byte payloadVersion)
    {
        ReadEntry entry;
        switch (transferKind)
        {
            case TaskHostTaskParameterTransferKind.CachedValue:
                if (payload is null)
                {
                    throw new InvalidOperationException("A cached TaskHost parameter payload was received without data.");
                }

                entry = new ReadEntry(payload, payloadCompressed, payloadVersion);
                _readEntries[cacheId] = entry;
                break;

            case TaskHostTaskParameterTransferKind.CacheReference:
                if (!_readEntries.TryGetValue(cacheId, out entry))
                {
                    throw new InvalidOperationException($"TaskHost parameter cache entry '{cacheId}' was not found.");
                }

                break;

            case TaskHostTaskParameterTransferKind.UncachedValue:
                if (payload is null)
                {
                    throw new InvalidOperationException("An uncached TaskHost parameter payload was received without data.");
                }

                entry = new ReadEntry(payload, payloadCompressed, payloadVersion);
                break;

            default:
                throw new InvalidOperationException($"TaskHost parameter transfer kind '{transferKind}' cannot be applied from the cache.");
        }

        byte[] serialized = entry.Compressed ? Decompress(entry.Payload) : entry.Payload;
        return Deserialize(serialized, entry.PacketVersion);
    }

    public void Reset()
    {
        _writeEntries.Clear();
        _readEntries.Clear();
        _nextId = 0;
        _cachedBytes = 0;
    }

    private static byte[] Serialize(Dictionary<string, TaskParameter>? taskParameters, byte packetVersion)
    {
        using var stream = new MemoryStream();
        using ITranslator translator = BinaryTranslator.GetWriteTranslator(stream);
        translator.NegotiatedPacketVersion = packetVersion;
        translator.TranslateDictionary(
            ref taskParameters,
            StringComparer.OrdinalIgnoreCase,
            TaskParameter.FactoryForDeserialization);
        return stream.ToArray();
    }

    private static Dictionary<string, TaskParameter>? Deserialize(byte[] serialized, byte packetVersion)
    {
        using var stream = new MemoryStream(serialized, 0, serialized.Length, writable: false, publiclyVisible: true);
        using ITranslator translator = BinaryTranslator.GetReadTranslator(stream, InterningBinaryReader.PoolingBuffer);
        translator.NegotiatedPacketVersion = packetVersion;
        Dictionary<string, TaskParameter>? taskParameters = null;
        translator.TranslateDictionary(
            ref taskParameters,
            StringComparer.OrdinalIgnoreCase,
            TaskParameter.FactoryForDeserialization);
        return taskParameters;
    }

    private static byte[] Compress(byte[] serialized, out bool compressed)
    {
        if (serialized.Length < CompressionThreshold)
        {
            compressed = false;
            return serialized;
        }

        using var stream = new MemoryStream(serialized.Length);
        using (var compressor = new DeflateStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        {
            compressor.Write(serialized, 0, serialized.Length);
        }

        if (stream.Length >= serialized.Length)
        {
            compressed = false;
            return serialized;
        }

        compressed = true;
        return stream.ToArray();
    }

    private static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed, writable: false);
        using var decompressor = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        return output.ToArray();
    }

    private static ulong GetHash(byte[] bytes)
    {
        const ulong offsetBasis = 14695981039346656037;
        const ulong prime = 1099511628211;

        ulong hash = offsetBasis;
        foreach (byte value in bytes)
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }

    private sealed record WriteEntry(int Id, byte[] Serialized);

    private readonly record struct ReadEntry(byte[] Payload, bool Compressed, byte PacketVersion);
}

internal static class TaskHostDictionaryDelta
{
    public const byte MinimumPacketVersion = 5;
    public const byte TaskParameterCacheMinimumPacketVersion = 6;

    private static readonly StringComparer s_comparer = StringComparer.OrdinalIgnoreCase;

    public static bool IsEnabled
        => Environment.GetEnvironmentVariable("MSBUILDTASKHOSTCONFIGCACHE") == "1";

    public static void Prepare(
        IDictionary<string, string>? current,
        ref Dictionary<string, string>? baseline,
        out TaskHostDictionaryTransferKind transferKind,
        out Dictionary<string, string>? values,
        out List<string>? removedKeys)
    {
        current ??= new Dictionary<string, string>(s_comparer);
        values = null;
        removedKeys = null;

        if (baseline is null)
        {
            transferKind = TaskHostDictionaryTransferKind.Full;
            values = Clone(current);
            baseline = values;
            return;
        }

        Dictionary<string, string>? changes = null;
        int fullCost = 0;
        int deltaCost = 0;

        foreach (KeyValuePair<string, string> pair in current)
        {
            fullCost += EstimatePairCost(pair.Key, pair.Value);

            if (!baseline.TryGetValue(pair.Key, out string? previousValue) ||
                !String.Equals(previousValue, pair.Value, StringComparison.Ordinal))
            {
                changes ??= new Dictionary<string, string>(s_comparer);
                changes[pair.Key] = pair.Value;
                deltaCost += EstimatePairCost(pair.Key, pair.Value);
            }
        }

        foreach (string key in baseline.Keys)
        {
            if (!current.ContainsKey(key))
            {
                removedKeys ??= [];
                removedKeys.Add(key);
                deltaCost += key.Length + sizeof(int);
            }
        }

        if (changes is null && removedKeys is null)
        {
            transferKind = TaskHostDictionaryTransferKind.Unchanged;
        }
        else if (deltaCost < fullCost)
        {
            transferKind = TaskHostDictionaryTransferKind.Delta;
            values = changes;
        }
        else
        {
            transferKind = TaskHostDictionaryTransferKind.Full;
            values = Clone(current);
            removedKeys = null;
        }

        baseline = Clone(current);
    }

    public static Dictionary<string, string> Apply(
        TaskHostDictionaryTransferKind transferKind,
        Dictionary<string, string>? values,
        List<string>? removedKeys,
        ref Dictionary<string, string>? baseline)
    {
        Dictionary<string, string> result;

        switch (transferKind)
        {
            case TaskHostDictionaryTransferKind.Full:
                result = values is null
                    ? new Dictionary<string, string>(s_comparer)
                    : Clone(values);
                break;

            case TaskHostDictionaryTransferKind.Delta:
                if (baseline is null)
                {
                    throw new InvalidOperationException("A TaskHost dictionary delta was received without a baseline.");
                }

                result = Clone(baseline);
                if (values is not null)
                {
                    foreach (KeyValuePair<string, string> pair in values)
                    {
                        result[pair.Key] = pair.Value;
                    }
                }

                if (removedKeys is not null)
                {
                    foreach (string key in removedKeys)
                    {
                        result.Remove(key);
                    }
                }

                break;

            case TaskHostDictionaryTransferKind.Unchanged:
                if (baseline is null)
                {
                    throw new InvalidOperationException("An unchanged TaskHost dictionary was received without a baseline.");
                }

                result = Clone(baseline);
                break;

            default:
                throw new InvalidOperationException($"Unknown TaskHost dictionary transfer kind '{transferKind}'.");
        }

        baseline = result;
        return result;
    }

    public static Dictionary<string, string> Clone(IDictionary<string, string> source)
        => new(source, s_comparer);

    private static int EstimatePairCost(string key, string? value)
        => key.Length + (value?.Length ?? 0) + (2 * sizeof(int));
}
