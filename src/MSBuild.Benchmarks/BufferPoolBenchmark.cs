// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Build.Utilities;

namespace MSBuild.Benchmarks;

/// <summary>
///  Compares steady-state rentals from the shared array and buffer pools.
/// </summary>
/// <remarks>
///  BenchmarkDotNet's warmup primes the pools. The nested cases keep two buffers rented at once,
///  exercising either two different size buckets or a miss in the thread-local bucket.
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BufferPoolBenchmark
{
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SingleChar")]
    [Arguments(16)]
    [Arguments(64)]
    [Arguments(256)]
    [Arguments(1024)]
    public void ArrayPoolSingleChar(int length)
    {
        char[] buffer = ArrayPool<char>.Shared.Rent(length);
        ArrayPool<char>.Shared.Return(buffer);
    }

    [Benchmark]
    [BenchmarkCategory("SingleChar")]
    [Arguments(16)]
    [Arguments(64)]
    [Arguments(256)]
    [Arguments(1024)]
    public void BufferPoolSingleChar(int length)
    {
        char[] buffer = BufferPool<char>.Shared.Rent(length);
        BufferPool<char>.Shared.Return(buffer);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SingleString")]
    [Arguments(16)]
    [Arguments(64)]
    [Arguments(256)]
    [Arguments(1024)]
    public void ArrayPoolSingleString(int length)
    {
        string[] buffer = ArrayPool<string>.Shared.Rent(length);
        ArrayPool<string>.Shared.Return(buffer);
    }

    [Benchmark]
    [BenchmarkCategory("SingleString")]
    [Arguments(16)]
    [Arguments(64)]
    [Arguments(256)]
    [Arguments(1024)]
    public void BufferPoolSingleString(int length)
    {
        string[] buffer = BufferPool<string>.Shared.Rent(length);
        BufferPool<string>.Shared.Return(buffer);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NestedSameSizeChar")]
    public void ArrayPoolNestedSameSizeChar()
    {
        char[] outer = ArrayPool<char>.Shared.Rent(256);
        char[] inner = ArrayPool<char>.Shared.Rent(256);
        ArrayPool<char>.Shared.Return(inner);
        ArrayPool<char>.Shared.Return(outer);
    }

    [Benchmark]
    [BenchmarkCategory("NestedSameSizeChar")]
    public void BufferPoolNestedSameSizeChar()
    {
        char[] outer = BufferPool<char>.Shared.Rent(256);
        char[] inner = BufferPool<char>.Shared.Rent(256);
        BufferPool<char>.Shared.Return(inner);
        BufferPool<char>.Shared.Return(outer);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NestedSameSizeString")]
    public void ArrayPoolNestedSameSizeString()
    {
        string[] outer = ArrayPool<string>.Shared.Rent(256);
        string[] inner = ArrayPool<string>.Shared.Rent(256);
        ArrayPool<string>.Shared.Return(inner);
        ArrayPool<string>.Shared.Return(outer);
    }

    [Benchmark]
    [BenchmarkCategory("NestedSameSizeString")]
    public void BufferPoolNestedSameSizeString()
    {
        string[] outer = BufferPool<string>.Shared.Rent(256);
        string[] inner = BufferPool<string>.Shared.Rent(256);
        BufferPool<string>.Shared.Return(inner);
        BufferPool<string>.Shared.Return(outer);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NestedMixedSizesChar")]
    public void ArrayPoolNestedMixedSizesChar()
    {
        char[] outer = ArrayPool<char>.Shared.Rent(256);
        char[] inner = ArrayPool<char>.Shared.Rent(16);
        ArrayPool<char>.Shared.Return(inner);
        ArrayPool<char>.Shared.Return(outer);
    }

    [Benchmark]
    [BenchmarkCategory("NestedMixedSizesChar")]
    public void BufferPoolNestedMixedSizesChar()
    {
        char[] outer = BufferPool<char>.Shared.Rent(256);
        char[] inner = BufferPool<char>.Shared.Rent(16);
        BufferPool<char>.Shared.Return(inner);
        BufferPool<char>.Shared.Return(outer);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NestedMixedSizesString")]
    public void ArrayPoolNestedMixedSizesString()
    {
        string[] outer = ArrayPool<string>.Shared.Rent(256);
        string[] inner = ArrayPool<string>.Shared.Rent(16);
        ArrayPool<string>.Shared.Return(inner);
        ArrayPool<string>.Shared.Return(outer);
    }

    [Benchmark]
    [BenchmarkCategory("NestedMixedSizesString")]
    public void BufferPoolNestedMixedSizesString()
    {
        string[] outer = BufferPool<string>.Shared.Rent(256);
        string[] inner = BufferPool<string>.Shared.Rent(16);
        BufferPool<string>.Shared.Return(inner);
        BufferPool<string>.Shared.Return(outer);
    }
}
