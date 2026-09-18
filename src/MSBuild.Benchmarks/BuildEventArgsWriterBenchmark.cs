// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures serialization of <see cref="ProjectStartedEventArgs"/> through
/// <see cref="BuildEventArgsWriter"/>, which is what the binary logger uses to write
/// events to a .binlog. Each project-started event carries a distinct property bag, so
/// every event produces a distinct NameValueList record (the writer deduplicates identical
/// bags by hash). This exercises the <c>WriteNameValueListRecord</c> path, which is the
/// hot path for builds with many distinct metadata/property bags.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(InProcessConfig))]
public class BuildEventArgsWriterBenchmark
{
    /// <summary>
    /// Runs the benchmark in-process (no child project build). MSBuild builds with a
    /// bootstrapped SDK to an unconventional artifacts layout, which BenchmarkDotNet's
    /// default toolchain cannot compile its auto-generated boilerplate against. The
    /// in-process emit toolchain sidesteps that while still performing real (non-Dry)
    /// warmup/iteration measurement with the memory diagnoser.
    /// </summary>
    private sealed class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            AddJob(Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithWarmupCount(10)
                .WithIterationCount(15));
            AddDiagnoser(MemoryDiagnoser.Default);
        }
    }

    /// <summary>
    /// Number of distinct project-started events (each with a distinct property bag) to write.
    /// </summary>
    [Params(1000)]
    public int EventCount { get; set; }

    /// <summary>
    /// Number of properties in each distinct bag.
    /// </summary>
    [Params(20)]
    public int PropertiesPerEvent { get; set; }

    private List<ProjectStartedEventArgs> _events = null!;

    [GlobalSetup]
    public void Setup()
    {
        _events = new List<ProjectStartedEventArgs>(EventCount);

        for (int i = 0; i < EventCount; i++)
        {
            var properties = new List<DictionaryEntry>(PropertiesPerEvent);
            for (int j = 0; j < PropertiesPerEvent; j++)
            {
                // Distinct keys/values per event so each bag hashes uniquely and a
                // new NameValueList record is written for every event.
                properties.Add(new DictionaryEntry($"Prop_{i}_{j}", $"Value_{i}_{j}"));
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

            _events.Add(args);
        }
    }

    /// <summary>
    /// Serializes every event through a single <see cref="BuildEventArgsWriter"/> instance,
    /// mirroring how the binary logger writes a build. A fresh writer is created per invocation
    /// so its NameValueList dedup cache starts empty and every event yields a distinct record.
    /// </summary>
    [Benchmark]
    public long SerializeDistinctPropertyBags()
    {
        using var stream = new MemoryStream(1 << 20);
        using var binaryWriter = new BinaryWriter(stream);
        var writer = new BuildEventArgsWriter(binaryWriter);

        for (int i = 0; i < _events.Count; i++)
        {
            writer.Write(_events[i]);
        }

        binaryWriter.Flush();
        return stream.Length;
    }
}
