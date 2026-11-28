// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Threading;
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

    [Fact]
    public void Shared_CacheIsIsolatedPerThread()
    {
        ArrayPool<TestBufferElement> pool = BufferPool<TestBufferElement>.Shared;
        TestBufferElement[] cachedBuffer = pool.Rent(16);
        pool.Return(cachedBuffer);

        TestBufferElement[]? otherThreadBuffer = null;
        Exception? threadException = null;
        Thread thread = new(() =>
        {
            try
            {
                TestBufferElement[] buffer = pool.Rent(16);
                otherThreadBuffer = buffer;
                pool.Return(buffer);
            }
            catch (Exception exception)
            {
                threadException = exception;
            }
        });

        thread.Start();
        thread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();

        threadException.ShouldBeNull();
        otherThreadBuffer.ShouldNotBeSameAs(cachedBuffer);

        TestBufferElement[] rentedBuffer = pool.Rent(16);
        rentedBuffer.ShouldBeSameAs(cachedBuffer);
        pool.Return(rentedBuffer);
    }

    [Fact]
    public void Shared_SimultaneousRentalsOnDifferentThreadsAreDistinct()
    {
        ArrayPool<TestBufferElement> pool = BufferPool<TestBufferElement>.Shared;
        using Barrier barrier = new(2);
        TestBufferElement[]?[] buffers = new TestBufferElement[]?[2];
        bool[] reachedBarrier = new bool[2];
        Exception?[] threadExceptions = new Exception?[2];

        void RentAndWait(int index)
        {
            TestBufferElement[]? buffer = null;
            try
            {
                buffer = pool.Rent(16);
                buffers[index] = buffer;
                reachedBarrier[index] = barrier.SignalAndWait(TimeSpan.FromSeconds(10));
            }
            catch (Exception exception)
            {
                threadExceptions[index] = exception;
            }
            finally
            {
                if (buffer is not null)
                {
                    pool.Return(buffer);
                }
            }
        }

        Thread firstThread = new(() => RentAndWait(0));
        Thread secondThread = new(() => RentAndWait(1));
        firstThread.Start();
        secondThread.Start();

        firstThread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        secondThread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();

        threadExceptions[0].ShouldBeNull();
        threadExceptions[1].ShouldBeNull();
        reachedBarrier[0].ShouldBeTrue();
        reachedBarrier[1].ShouldBeTrue();
        buffers[0].ShouldNotBeSameAs(buffers[1]);
    }

    [Fact]
    public void Shared_CrossThreadReturnCachesOnReturningThread()
    {
        ArrayPool<TestBufferElement> pool = BufferPool<TestBufferElement>.Shared;
        using ManualResetEventSlim bufferRented = new();
        using ManualResetEventSlim allowOwnerToRentAgain = new();
        TestBufferElement[]? originalBuffer = null;
        TestBufferElement[]? ownerRentedBuffer = null;
        Exception? threadException = null;

        Thread ownerThread = new(() =>
        {
            try
            {
                originalBuffer = pool.Rent(16);
                bufferRented.Set();
                allowOwnerToRentAgain.Wait();
                TestBufferElement[] buffer = pool.Rent(16);
                ownerRentedBuffer = buffer;
                pool.Return(buffer);
            }
            catch (Exception exception)
            {
                threadException = exception;
                bufferRented.Set();
            }
        });

        ownerThread.Start();
        bool ownerRented = bufferRented.Wait(TimeSpan.FromSeconds(10));
        TestBufferElement[]? returnedThreadBuffer = null;
        try
        {
            if (ownerRented)
            {
                pool.Return(originalBuffer!);
                returnedThreadBuffer = pool.Rent(16);
            }
        }
        finally
        {
            allowOwnerToRentAgain.Set();
        }

        ownerThread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();

        threadException.ShouldBeNull();
        ownerRented.ShouldBeTrue();
        returnedThreadBuffer.ShouldBeSameAs(originalBuffer);
        ownerRentedBuffer.ShouldNotBeSameAs(originalBuffer);
        pool.Return(returnedThreadBuffer!);
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
