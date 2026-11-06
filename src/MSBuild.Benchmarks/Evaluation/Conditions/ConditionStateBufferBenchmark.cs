// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

[BenchmarkCategory("Conditions", "ConditionState")]
[MemoryDiagnoser]
public class ConditionStateBufferBenchmark
{
    [Params(256, 258)]
    public int Slots { get; set; }

    [GlobalSetup]
    public void Setup() => RentAndReturn();

    [Benchmark]
    public void RentAndReturn()
    {
        StringExpressionNode.EvaluationState[] buffer =
            ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Acquire(Slots);
        buffer[0].ExpandedValue = "first";
        buffer[Slots - 1].ExpandedValue = "last";
        ThreadLocalNodeStateBuffer<StringExpressionNode.EvaluationState>.Release(buffer, Slots);
    }
}
