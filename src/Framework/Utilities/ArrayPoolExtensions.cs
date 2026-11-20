// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;

namespace Microsoft.Build.Utilities;

internal static class ArrayPoolExtensions
{
    /// <summary>
    ///  Rents an array from <paramref name="pool"/> and returns it when the returned scope is disposed.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <param name="pool">The pool to rent from.</param>
    /// <param name="minimumLength">The minimum required array length.</param>
    /// <param name="array">The rented array, which may be longer than <paramref name="minimumLength"/>.</param>
    /// <returns>
    ///  A scope that returns the array to <paramref name="pool"/> when disposed.
    /// </returns>
    /// <remarks>
    ///  The entire array is cleared when <typeparamref name="T"/> is or contains references.
    /// </remarks>
    public static PooledBuffer<T> Rent<T>(this ArrayPool<T> pool, int minimumLength, out T[] array)
    {
        ArgumentNullException.ThrowIfNull(pool);

        array = pool.Rent(minimumLength);
        return new(pool, array, array.Length, array.Length);
    }

    /// <summary>
    ///  Rents an array from <paramref name="pool"/> and returns it when the returned scope is disposed.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <param name="pool">The pool to rent from.</param>
    /// <param name="minimumLength">The minimum required array length.</param>
    /// <param name="clearLength">
    ///  The number of leading elements to clear when <typeparamref name="T"/> is or contains references, from zero through
    ///  <paramref name="minimumLength"/>.
    /// </param>
    /// <param name="array">The rented array, which may be longer than <paramref name="minimumLength"/>.</param>
    /// <returns>
    ///  A scope that returns the array to <paramref name="pool"/> when disposed.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  <paramref name="clearLength"/> is negative or greater than <paramref name="minimumLength"/>.
    /// </exception>
    /// <remarks>
    ///  Pass <paramref name="clearLength"/> only when every element the caller may populate with references lies within
    ///  that prefix.
    /// </remarks>
    public static PooledBuffer<T> Rent<T>(this ArrayPool<T> pool, int minimumLength, int clearLength, out T[] array)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentOutOfRangeException.ThrowIfNegative(clearLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(clearLength, minimumLength);

        array = pool.Rent(minimumLength);
        return new(pool, array, array.Length, clearLength);
    }

    /// <summary>
    ///  Rents an array from <paramref name="pool"/> and returns the requested-length span. The array is returned
    ///  to the pool when the returned scope is disposed.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="pool">The pool to rent from.</param>
    /// <param name="length">The required span length.</param>
    /// <param name="span">A span of exactly <paramref name="length"/> elements.</param>
    /// <returns>
    ///  A scope that returns the array to <paramref name="pool"/> when disposed.
    /// </returns>
    /// <remarks>
    ///  The span is cleared when <typeparamref name="T"/> is or contains references.
    /// </remarks>
    public static PooledBuffer<T> RentSpan<T>(this ArrayPool<T> pool, int length, out Span<T> span)
    {
        ArgumentNullException.ThrowIfNull(pool);

        T[] array = pool.Rent(length);
        span = array.AsSpan(0, length);
        return new(pool, array, length, length);
    }

    /// <summary>
    ///  Rents an array from <paramref name="pool"/> and returns the requested-length span. The array is returned
    ///  to the pool when the returned scope is disposed.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="pool">The pool to rent from.</param>
    /// <param name="length">The required span length.</param>
    /// <param name="clearLength">
    ///  The number of leading elements to clear when <typeparamref name="T"/> is or contains references, from zero through
    ///  <paramref name="length"/>.
    /// </param>
    /// <param name="span">A span of exactly <paramref name="length"/> elements.</param>
    /// <returns>
    ///  A scope that returns the array to <paramref name="pool"/> when disposed.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  <paramref name="clearLength"/> is negative or greater than <paramref name="length"/>.
    /// </exception>
    /// <remarks>
    ///  Pass <paramref name="clearLength"/> only when every element the caller may populate with references lies within
    ///  that prefix.
    /// </remarks>
    public static PooledBuffer<T> RentSpan<T>(this ArrayPool<T> pool, int length, int clearLength, out Span<T> span)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentOutOfRangeException.ThrowIfNegative(clearLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(clearLength, length);

        T[] array = pool.Rent(length);
        span = array.AsSpan(0, length);
        return new(pool, array, length, clearLength);
    }
}
