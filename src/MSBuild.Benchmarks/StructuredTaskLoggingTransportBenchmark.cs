// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures dedicated structured event construction and node-packet serialization.
/// </summary>
[MemoryDiagnoser]
public class StructuredTaskLoggingTransportBenchmark
{
    private static readonly KeyValuePair<string, string?>[] s_values =
    [
        new("Candidate", "candidate.dll"),
        new("Expected", "expected.dll"),
    ];

    private StructuredBuildMessageEventArgs _source = null!;
    private MemoryStream _writeStream = null!;
    private BinaryWriter _writer = null!;
    private MemoryStream _readStream = null!;
    private BinaryReader _reader = null!;

    [GlobalSetup]
    public void Setup()
    {
        _source = CreateStructuredEvent();
        _writeStream = new MemoryStream();
        _writer = new BinaryWriter(_writeStream);
        _source.WriteToStream(_writer);
        _writer.Flush();
        _readStream = new MemoryStream(_writeStream.ToArray());
        _reader = new BinaryReader(_readStream);
    }

    [Benchmark(Baseline = true)]
    public BuildMessageEventArgs ClassicEvent() =>
        new BuildMessageEventArgs(
            "Considered {0} but expected {1}",
            null,
            "Benchmark",
            MessageImportance.Low,
            DateTime.UtcNow,
            "candidate.dll",
            "expected.dll");

    [Benchmark]
    public BuildMessageEventArgs StructuredEvent() =>
        CreateStructuredEvent();

    [Benchmark]
    public long SerializeStructuredNodeEvent()
    {
        _writeStream.Position = 0;
        _writeStream.SetLength(0);
        _source.WriteToStream(_writer);
        return _writeStream.Length;
    }

    [Benchmark]
    public BuildMessageEventArgs DeserializeStructuredNodeEvent()
    {
        _readStream.Position = 0;
        var result = new StructuredBuildMessageEventArgs();
        result.CreateFromStream(_reader, Environment.Version.Major * 10);
        return result;
    }

    private static StructuredBuildMessageEventArgs CreateStructuredEvent() =>
        new(
            null,
            null,
            null,
            0,
            0,
            0,
            0,
            "Considered {Candidate} but expected {Expected}",
            "Considered {Candidate} but expected {Expected}",
            s_values,
            null,
            "Benchmark",
            MessageImportance.Low,
            DateTime.UtcNow);
}
