// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace MSBuild.Benchmarks;

#if NETFRAMEWORK && EVALUATION_OBSERVATION_DETOURS
[Config(typeof(EvaluationObservationBenchmarkConfiguration))]
public partial class EvaluationObservationBenchmark
{
    [Benchmark]
    public long NativeAndDetours() => Run(EvaluationObservationBenchmarkMode.NativeAndDetours);
}

internal sealed class EvaluationObservationBenchmarkConfiguration : ManualConfig
{
    public EvaluationObservationBenchmarkConfiguration()
    {
        AddJob(new Job().WithPlatform(Platform.X64).AsMutator());
    }
}
#endif
