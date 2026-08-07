// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation;
using Microsoft.NET.StringTools;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures cold-cache ExpressionShredder allocations that steady-state interning would hide.
/// </summary>
/// <remarks>
///  One invocation is measured per iteration. The intern cache is cleared outside the measured
///  invocation so each fragment is materialized without adding a benchmark-only branch to WeakIntern.
/// </remarks>
[BenchmarkCategory("ExpressionShredderAllocations")]
[MemoryDiagnoser]
[RunOncePerIteration]
public class ExpressionShredderAllocationBenchmark
{
    private const int ExpressionCount = 64;

    private readonly string[] _itemExpressions = new string[ExpressionCount];
    private readonly string[] _splitExpressions = new string[ExpressionCount];

    [GlobalSetup]
    public void GlobalSetup()
    {
        for (int i = 0; i < ExpressionCount; i++)
        {
            _itemExpressions[i] = $"@(Compile{i}->'%(Filename{i})'->Distinct{i}()->Reverse{i}())";
            _splitExpressions[i] =
                $"@(Compile{i}->'%(FullPath{i})', ';');$(A{i});$(B{i});value{i}a;value{i}b;" +
                $"@(Reference{i});%(Culture{i});@(Content{i}->'%(Filename{i})', ';')";
        }
    }

    [IterationSetup]
    public void IterationSetup()
        => Strings.ClearCachedStrings();

    [Benchmark(OperationsPerInvoke = ExpressionCount)]
    public int ItemExpressions()
    {
        int count = 0;

        for (int i = 0; i < _itemExpressions.Length; i++)
        {
            ExpressionShredder.ReferencedItemExpressionsEnumerator enumerator =
                ExpressionShredder.GetReferencedItemExpressions(_itemExpressions[i]);

            while (enumerator.MoveNext())
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark(OperationsPerInvoke = ExpressionCount)]
    public int Split()
    {
        int count = 0;

        for (int i = 0; i < _splitExpressions.Length; i++)
        {
            foreach (string _ in ExpressionShredder.SplitSemiColonSeparatedList(_splitExpressions[i]))
            {
                count++;
            }
        }

        return count;
    }
}
