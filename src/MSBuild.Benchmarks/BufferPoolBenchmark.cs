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
/// <typeparam name="T">The type of elements in the rented buffers.</typeparam>
/// <remarks>
///  BenchmarkDotNet's warmup primes the pools. The nested cases keep two buffers rented at once,
///  exercising either two different size buckets or a miss in the thread-local bucket.
/// </remarks>
[GenericTypeArguments(typeof(char))]
[GenericTypeArguments(typeof(string))]
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BufferPoolBenchmark<T>
{
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Single")]
    [Arguments(16)]
    [Arguments(64)]
    [Arguments(256)]
    [Arguments(1024)]
    public void ArrayPoolSingle(int length)
    {
        T[] buffer = ArrayPool<T>.Shared.Rent(length);
        ArrayPool<T>.Shared.Return(buffer);
    }

    [Benchmark]
    [BenchmarkCategory("Single")]
    [Arguments(16)]
    [Arguments(64)]
    [Arguments(256)]
    [Arguments(1024)]
    public void BufferPoolSingle(int length)
    {
        T[] buffer = BufferPool<T>.Shared.Rent(length);
        BufferPool<T>.Shared.Return(buffer);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NestedSameSize")]
    public void ArrayPoolNestedSameSize()
    {
        T[] outer = ArrayPool<T>.Shared.Rent(256);
        T[] inner = ArrayPool<T>.Shared.Rent(256);
        ArrayPool<T>.Shared.Return(inner);
        ArrayPool<T>.Shared.Return(outer);
    }

    [Benchmark]
    [BenchmarkCategory("NestedSameSize")]
    public void BufferPoolNestedSameSize()
    {
        T[] outer = BufferPool<T>.Shared.Rent(256);
        T[] inner = BufferPool<T>.Shared.Rent(256);
        BufferPool<T>.Shared.Return(inner);
        BufferPool<T>.Shared.Return(outer);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NestedMixedSizes")]
    public void ArrayPoolNestedMixedSizes()
    {
        T[] outer = ArrayPool<T>.Shared.Rent(256);
        T[] inner = ArrayPool<T>.Shared.Rent(16);
        ArrayPool<T>.Shared.Return(inner);
        ArrayPool<T>.Shared.Return(outer);
    }

    [Benchmark]
    [BenchmarkCategory("NestedMixedSizes")]
    public void BufferPoolNestedMixedSizes()
    {
        T[] outer = BufferPool<T>.Shared.Rent(256);
        T[] inner = BufferPool<T>.Shared.Rent(16);
        BufferPool<T>.Shared.Return(inner);
        BufferPool<T>.Shared.Return(outer);
    }
}
