// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;

namespace Microsoft.Build.Utilities;

/// <summary>
///  Returns a rented array to its pool when disposed.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
/// <param name="owner">The pool that owns the array.</param>
/// <param name="array">The array to return to the pool.</param>
/// <param name="length">The length of <see cref="Span"/>.</param>
/// <param name="clearLength">The number of leading elements to clear before returning the array.</param>
/// <remarks>
///  Use directly in a <see langword="using"/> statement. Do not copy this scope or use its array or span after disposal.
/// </remarks>
internal ref struct PooledBuffer<T>(ArrayPool<T> owner, T[] array, int length, int clearLength)
{
    private readonly ArrayPool<T> _owner = owner;
    private readonly int _length = length;
    private int _clearLength = clearLength;
    private T[]? _array = array;

    /// <summary>
    ///  Gets the entire rented array, which may be longer than <see cref="Span"/>.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The scope has been disposed.</exception>
    public readonly T[] Array
    {
        get
        {
            T[]? array = _array;
            ObjectDisposedException.ThrowIf(array is null, typeof(PooledBuffer<T>));

            return array;
        }
    }

    /// <summary>
    ///  Gets the span requested from <c>RentSpan</c>, or the entire array when rented with <c>Rent</c>.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The scope has been disposed.</exception>
    public readonly Span<T> Span => Array.AsSpan(0, _length);

    /// <summary>
    ///  Gets the number of leading elements to clear on disposal when <typeparamref name="T"/> is or contains references.
    /// </summary>
    public readonly int ClearLength => _clearLength;

    /// <summary>
    ///  Sets the number of leading elements to clear on disposal when <typeparamref name="T"/> is or contains references.
    /// </summary>
    /// <param name="clearLength">The number of leading elements to clear, from zero through the length of <see cref="Array"/>.</param>
    /// <exception cref="ObjectDisposedException">The scope has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  <paramref name="clearLength"/> is negative or greater than the length of <see cref="Array"/>.
    /// </exception>
    /// <remarks>
    ///  Use this when the populated range is not known when renting. The value may exceed the length of <see cref="Span"/>
    ///  when the caller writes through <see cref="Array"/>. Unlike a property setter, this method can be called on a
    ///  <see langword="using"/> variable.
    /// </remarks>
    public void SetClearLength(int clearLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(clearLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(clearLength, Array.Length);

        _clearLength = clearLength;
    }

    /// <summary>
    ///  Clears the first <see cref="ClearLength"/> elements when <typeparamref name="T"/> is or contains references,
    ///  then returns the array to its pool.
    /// </summary>
    public void Dispose()
    {
        T[]? array = _array;
        if (array is null)
        {
            return;
        }

        _array = null;

        if (TypeInfo<T>.IsReferenceOrContainsReferences())
        {
            System.Array.Clear(array, 0, _clearLength);
        }

        _owner.Return(array, clearArray: false);
    }
}
