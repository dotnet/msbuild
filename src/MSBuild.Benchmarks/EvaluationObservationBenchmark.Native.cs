// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

public partial class EvaluationObservationBenchmark
{
    [Benchmark]
    public long Native() => Run(EvaluationObservationBenchmarkMode.Native);
}
