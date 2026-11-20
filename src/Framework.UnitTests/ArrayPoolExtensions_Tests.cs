// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

/// <summary>
///  Tests for <see cref="ArrayPoolExtensions"/>.
/// </summary>
public class ArrayPoolExtensions_Tests
{
    [Fact]
    public void RentSpan_ExposesRequestedLengthAndClearsOnlyTheSpan()
    {
        object existingReference = new();
        object[] buffer = [existingReference, existingReference, existingReference, existingReference];
        TrackingArrayPool<object> pool = new(buffer);

        using (pool.RentSpan(2, out Span<object> span))
        {
            span.Length.ShouldBe(2);
            span[0] = new object();
            span[1] = new object();
        }

        pool.ReturnedArray.ShouldBeSameAs(buffer);
        pool.ClearArray.ShouldBe(false);
        buffer[0].ShouldBeNull();
        buffer[1].ShouldBeNull();
        buffer[2].ShouldBeSameAs(existingReference);
        buffer[3].ShouldBeSameAs(existingReference);
    }

    [Fact]
    public void Rent_ClearsEntireArrayForReferenceElements()
    {
        string[] buffer = ["first", "second", "third", "fourth"];
        TrackingArrayPool<string> pool = new(buffer);

        using (pool.Rent(2, out string[] rented))
        {
            rented.Length.ShouldBe(4);
        }

        pool.ReturnedArray.ShouldBeSameAs(buffer);
        foreach (string value in buffer)
        {
            value.ShouldBeNull();
        }
    }

    [Fact]
    public void Rent_ClearLengthLimitsClearedPrefix()
    {
        string[] buffer = ["first", "second", "third", "fourth"];
        TrackingArrayPool<string> pool = new(buffer);

        using (pool.Rent(2, clearLength: 1, out _))
        {
        }

        pool.ClearArray.ShouldBe(false);
        buffer[0].ShouldBeNull();
        buffer[1].ShouldBe("second");
        buffer[2].ShouldBe("third");
        buffer[3].ShouldBe("fourth");
    }

    [Fact]
    public void RentSpan_ZeroClearLengthSkipsClearing()
    {
        string[] buffer = ["first", "second"];
        TrackingArrayPool<string> pool = new(buffer);

        using (pool.RentSpan(2, clearLength: 0, out _))
        {
        }

        buffer[0].ShouldBe("first");
        buffer[1].ShouldBe("second");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void RentAndRentSpan_RejectClearLengthOutsideRequestedLengthBeforeRenting(int clearLength)
    {
        TrackingArrayPool<string> pool = new(new string[4]);

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            pool.Rent(2, clearLength, out _).Dispose());
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            pool.RentSpan(2, clearLength, out _).Dispose());
        pool.RentCount.ShouldBe(0);
    }

    [Fact]
    public void Properties_ExposeArrayAndRequestedSpan()
    {
        string[] buffer = new string[4];
        TrackingArrayPool<string> pool = new(buffer);

        using PooledBuffer<string> spanScope = pool.RentSpan(2, out _);
        spanScope.Array.ShouldBeSameAs(buffer);
        spanScope.Span.Length.ShouldBe(2);

        using PooledBuffer<string> arrayScope = pool.Rent(2, out _);
        arrayScope.Span.Length.ShouldBe(4);
    }

    [Fact]
    public void SetClearLength_OnUsingVariableCanExtendBeyondSpan()
    {
        string[] buffer = ["first", "second", "third", "fourth"];
        TrackingArrayPool<string> pool = new(buffer);

        using (PooledBuffer<string> scope = pool.RentSpan(1, out _))
        {
            scope.ClearLength.ShouldBe(1);
            scope.SetClearLength(3);
            scope.ClearLength.ShouldBe(3);
        }

        buffer[0].ShouldBeNull();
        buffer[1].ShouldBeNull();
        buffer[2].ShouldBeNull();
        buffer[3].ShouldBe("fourth");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void SetClearLength_RejectsValueOutsideArray(int clearLength)
    {
        TrackingArrayPool<string> pool = new(new string[4]);

        using PooledBuffer<string> scope = pool.RentSpan(1, out _);

        // PooledBuffer<T> is a ref struct and cannot be captured in a Should.Throw lambda.
        ArgumentOutOfRangeException? exception = null;
        try
        {
            scope.SetClearLength(clearLength);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            exception = ex;
        }

        _ = exception.ShouldNotBeNull();
    }

    [Fact]
    public void Properties_ThrowAfterDispose()
    {
        TrackingArrayPool<string> pool = new(new string[4]);

        PooledBuffer<string> scope = pool.RentSpan(1, out _);
        scope.Dispose();

        // PooledBuffer<T> is a ref struct and cannot be captured in Should.Throw lambdas.
        bool arrayThrew = false;
        try
        {
            _ = scope.Array;
        }
        catch (ObjectDisposedException)
        {
            arrayThrew = true;
        }

        bool spanThrew = false;
        try
        {
            _ = scope.Span;
        }
        catch (ObjectDisposedException)
        {
            spanThrew = true;
        }

        arrayThrew.ShouldBeTrue();
        spanThrew.ShouldBeTrue();
    }

    [Fact]
    public void Rent_DoesNotRequestClearingForValueElements()
    {
        int[] buffer = [1, 2, 3, 4];
        TrackingArrayPool<int> pool = new(buffer);

        using (pool.Rent(2, out int[] rented))
        {
            rented.Length.ShouldBe(4);
        }

        pool.ClearArray.ShouldBe(false);
    }

    private sealed class TrackingArrayPool<T>(T[] buffer) : ArrayPool<T>
    {
        public T[]? ReturnedArray { get; private set; }

        public bool? ClearArray { get; private set; }

        public int RentCount { get; private set; }

        public override T[] Rent(int minimumLength)
        {
            RentCount++;

            minimumLength.ShouldBeLessThanOrEqualTo(buffer.Length);

            return buffer;
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            ReturnedArray = array;
            ClearArray = clearArray;

            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }
        }
    }
}
