// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

/// <summary>
///  Tests for <see cref="BufferPool{T}"/>.
/// </summary>
public class BufferPool_Tests
{
    [Fact]
    public void Return_CachesAndClearsBuffer()
    {
        ArrayPool<TestBufferElement> pool = BufferPool<TestBufferElement>.Shared;
        TestBufferElement[] buffer = pool.Rent(1);
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = new TestBufferElement();
        }

        pool.Return(buffer, clearArray: true);

        TestBufferElement[] rented = pool.Rent(1);
#if NETFRAMEWORK
        rented.ShouldBeSameAs(buffer);
#endif
        foreach (TestBufferElement element in rented)
        {
            element.ShouldBeNull();
        }

        pool.Return(rented);
    }

#if NETFRAMEWORK
    [Theory]
    [InlineData(1, 16)]
    [InlineData(16, 16)]
    [InlineData(17, 32)]
    [InlineData(32, 32)]
    [InlineData(33, 64)]
    [InlineData(64, 64)]
    [InlineData(65, 128)]
    [InlineData(128, 128)]
    [InlineData(129, 256)]
    [InlineData(256, 256)]
    public void Shared_CachesEachSizeBucket(int minimumLength, int expectedLength)
    {
        ArrayPool<TestBufferElement> pool = BufferPool<TestBufferElement>.Shared;
        TestBufferElement[][] buffers = [pool.Rent(1), pool.Rent(17), pool.Rent(33), pool.Rent(65), pool.Rent(129)];
        foreach (TestBufferElement[] buffer in buffers)
        {
            pool.Return(buffer);
        }

        TestBufferElement[] rented = pool.Rent(minimumLength);
        rented.Length.ShouldBe(expectedLength);
        rented.ShouldBeSameAs(Array.Find(buffers, buffer => buffer.Length == expectedLength));
        pool.Return(rented);
    }

    [Fact]
    public void Shared_CachesMostRecentlyReturnedBuffer()
    {
        ArrayPool<TestBufferElement> pool = BufferPool<TestBufferElement>.Shared;
        TestBufferElement[] first = pool.Rent(16);
        TestBufferElement[] second = pool.Rent(16);
        pool.Return(first);
        pool.Return(second);

        TestBufferElement[] rented = pool.Rent(16);
        rented.ShouldBeSameAs(second);
        TestBufferElement[] next = pool.Rent(16);
        next.ShouldNotBeSameAs(rented);
        pool.Return(rented);
        pool.Return(next);
    }
#endif

    [Fact]
    public void Return_InvalidArgumentsThrow()
    {
        ArrayPool<TestBufferElement> pool = BufferPool<TestBufferElement>.Shared;
        _ = Should.Throw<ArgumentNullException>(()
            => pool.Return(null!));

        TestBufferElement[] buffer = pool.Rent(1);
        pool.Return(buffer);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(255)]
    public void Shared_ReturnRejectsInvalidBufferLength(int length)
        => Should.Throw<ArgumentException>(()
            => BufferPool<TestBufferElement>.Shared.Return(new TestBufferElement[length]));

    [Fact]
    public void Shared_DelegatesLargeBuffersToArrayPool()
    {
        ArrayPool<TestBufferElement> pool = BufferPool<TestBufferElement>.Shared;
        TestBufferElement[] buffer = pool.Rent(257);
        buffer.Length.ShouldBeGreaterThanOrEqualTo(257);
        buffer[0] = new TestBufferElement();
        pool.Return(buffer, clearArray: true);

        TestBufferElement[] rented = ArrayPool<TestBufferElement>.Shared.Rent(257);
        rented.ShouldBeSameAs(buffer);
        rented[0].ShouldBeNull();
        ArrayPool<TestBufferElement>.Shared.Return(rented);
    }

    [Fact]
    public void Rent_ZeroLengthReturnsEmptyBuffer()
    {
        ArrayPool<TestBufferElement> pool = BufferPool<TestBufferElement>.Shared;
        TestBufferElement[] buffer = pool.Rent(16);
        pool.Return(buffer);

        TestBufferElement[] empty = pool.Rent(0);
        empty.ShouldBeEmpty();
        pool.Return(empty);
    }

    [Fact]
    public void Rent_NegativeLengthThrowsEvenWhenCached()
    {
        ArrayPool<TestBufferElement> pool = BufferPool<TestBufferElement>.Shared;
        TestBufferElement[] buffer = pool.Rent(1);
        pool.Return(buffer);

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            pool.Rent(-1));
    }

    private sealed class TestBufferElement
    {
    }
}
