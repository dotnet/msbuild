// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace MSBuild.Benchmarks;

/// <summary>
/// Deterministic A/B harness for the BuildEventArgsWriter NameValueList BinaryWriter-reuse change.
///
/// This is a retained custom harness (not BenchmarkDotNet) used to produce publication-grade,
/// reproducible allocation evidence. Allocation counting via
/// <see cref="GC.GetAllocatedBytesForCurrentThread"/> is deterministic for this workload, so it
/// yields exact bytes/operation independent of statistical noise. The same source file is compiled
/// against both arms (exact-parent baseline and the fixed HEAD), so the harness bytes are identical
/// across arms; only the linked Microsoft.Build.dll differs. The loaded assembly path and hash are
/// emitted as provenance.
/// </summary>
public static class BinlogAllocationHarness
{
    private static readonly DateTime FixedTimestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly PropertyInfo RawTimestampProperty =
        typeof(BuildEventArgs).GetProperty("RawTimestamp", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        ?? throw new InvalidOperationException("Could not locate BuildEventArgs.RawTimestamp");

    /// <summary>
    /// Builds a list of ProjectStartedEventArgs with a fixed timestamp so serialized output is
    /// fully deterministic and comparable across arms.
    /// </summary>
    /// <param name="recordCount">Number of events to build.</param>
    /// <param name="propsPerEvent">Distinct properties per event.</param>
    /// <param name="valueLen">Approximate length of each property value string.</param>
    /// <param name="distinct">
    /// When true, every event gets a unique property bag (so every event produces a distinct
    /// NameValueList record). When false, all events share one identical bag (so the writer
    /// deduplicates to a single record — the fix then saves at most one allocation total).
    /// </param>
    private static List<ProjectStartedEventArgs> BuildEvents(int recordCount, int propsPerEvent, int valueLen, bool distinct)
    {
        var events = new List<ProjectStartedEventArgs>(recordCount);
        string pad = valueLen > 0 ? new string('x', valueLen) : string.Empty;

        for (int i = 0; i < recordCount; i++)
        {
            var properties = new List<DictionaryEntry>(propsPerEvent);
            for (int j = 0; j < propsPerEvent; j++)
            {
                if (distinct)
                {
                    properties.Add(new DictionaryEntry($"Prop_{i}_{j}", $"Value_{i}_{j}_{pad}"));
                }
                else
                {
                    properties.Add(new DictionaryEntry($"Prop_{j}", $"Value_{j}_{pad}"));
                }
            }

            var args = new ProjectStartedEventArgs(
                projectId: i,
                message: "Project started",
                helpKeyword: "help",
                projectFile: "project.proj",
                targetNames: "Build",
                properties: properties,
                items: new List<DictionaryEntry>(),
                parentBuildEventContext: BuildEventContext.Invalid,
                globalProperties: new Dictionary<string, string>(),
                toolsVersion: "Current")
            {
                BuildEventContext = new BuildEventContext(i, i, i, i),
            };

            RawTimestampProperty.SetValue(args, FixedTimestamp);
            events.Add(args);
        }

        return events;
    }

    /// <summary>
    /// Serializes all events through a single fresh BuildEventArgsWriter, mirroring one build's
    /// worth of writes. Returns the produced byte length.
    /// </summary>
    private static long SerializeOnce(List<ProjectStartedEventArgs> events)
    {
        using var stream = new MemoryStream(1 << 20);
        using var binaryWriter = new BinaryWriter(stream);
        var writer = new BuildEventArgsWriter(binaryWriter);
        for (int i = 0; i < events.Count; i++)
        {
            writer.Write(events[i]);
        }

        binaryWriter.Flush();
        return stream.Length;
    }

    /// <summary>
    /// Measures deterministic allocations (bytes/operation) for serializing the given workload,
    /// where one "operation" is serializing the whole event list through one writer.
    /// </summary>
    public static (long bytesPerOp, long outputLength) MeasureAllocations(
        int recordCount, int propsPerEvent, int valueLen, bool distinct, int warmup, int iterations)
    {
        var events = BuildEvents(recordCount, propsPerEvent, valueLen, distinct);

        long outputLength = 0;
        for (int i = 0; i < warmup; i++)
        {
            outputLength = SerializeOnce(events);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            SerializeOnce(events);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        return ((after - before) / iterations, outputLength);
    }

    /// <summary>
    /// Measures median wall-clock time (nanoseconds/operation) for serializing the workload.
    /// </summary>
    public static double MeasureTimeNs(
        int recordCount, int propsPerEvent, int valueLen, bool distinct, int warmup, int iterations)
    {
        var events = BuildEvents(recordCount, propsPerEvent, valueLen, distinct);

        for (int i = 0; i < warmup; i++)
        {
            SerializeOnce(events);
        }

        var samples = new double[iterations];
        var sw = new Stopwatch();
        for (int i = 0; i < iterations; i++)
        {
            sw.Restart();
            SerializeOnce(events);
            sw.Stop();
            samples[i] = sw.Elapsed.TotalMilliseconds * 1_000_000.0;
        }

        Array.Sort(samples);
        return samples[iterations / 2];
    }

    /// <summary>
    /// Serializes a fixed deterministic workload and returns a SHA-256 of the produced bytes plus
    /// the byte length. Used to prove the fixed and baseline arms emit byte-identical output.
    /// </summary>
    public static (string sha256, long length) OutputHash(int recordCount, int propsPerEvent, int valueLen, bool distinct)
    {
        var events = BuildEvents(recordCount, propsPerEvent, valueLen, distinct);
        using var stream = new MemoryStream(1 << 20);
        using var binaryWriter = new BinaryWriter(stream);
        var writer = new BuildEventArgsWriter(binaryWriter);
        for (int i = 0; i < events.Count; i++)
        {
            writer.Write(events[i]);
        }

        binaryWriter.Flush();
        byte[] bytes = stream.ToArray();
        byte[] hash = SHA256.HashData(bytes);
        return (Convert.ToHexString(hash), bytes.Length);
    }

    /// <summary>
    /// Emits provenance about the loaded Microsoft.Build assembly (path + SHA-256), which differs
    /// between the two arms and proves the harness measured different production binaries.
    /// </summary>
    public static string AssemblyProvenance()
    {
        Assembly asm = typeof(BuildEventArgsWriter).Assembly;
        string location = asm.Location;
        string sha = "<unknown>";
        try
        {
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                using var fs = File.OpenRead(location);
                sha = Convert.ToHexString(SHA256.HashData(fs));
            }
        }
        catch (IOException)
        {
        }

        return $"Microsoft.Build: {location}\n  sha256={sha}";
    }
}
