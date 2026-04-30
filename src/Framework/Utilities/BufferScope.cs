// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;

namespace Microsoft.Build.Utilities;

/// <summary>
///  Allows renting a buffer from <see cref="ArrayPool{T}"/> with a using statement. Can be used directly as if it
///  were a <see cref="Span{T}"/>.
/// </summary>
internal ref struct BufferScope<T>
{
    private T[]? _array;
    private Span<T> _span;

    /// <summary>
    ///  Initializes a new instance of the <see cref="BufferScope{T}"/>.
    /// </summary>
    /// <param name="minimumLength">The required minimum length.</param>
    public BufferScope(int minimumLength)
    {
        _array = ArrayPool<T>.Shared.Rent(minimumLength);
        _span = _array;
    }

    /// <summary>
    ///  Create the <see cref="BufferScope{T}"/> with an initial buffer. Useful for creating with an initial stack
    ///  allocated buffer.
    /// </summary>
    /// <param name="initialBuffer">The initial buffer to use.</param>
    public BufferScope(Span<T> initialBuffer)
    {
        _array = null;
        _span = initialBuffer;
    }

    /// <summary>
    ///  Create the <see cref="BufferScope{T}"/> with an initial buffer. Useful for creating with an initial stack
    ///  allocated buffer.
    /// </summary>
    /// <remarks>
    ///  <example>
    ///   Creating with a stack allocated buffer:
    ///   <code>using BufferScope&lt;char> buffer = new(stackalloc char[64]);</code>
    ///  </example>
    /// </remarks>
    /// <param name="initialBuffer">
    ///  The initial buffer to use. If not large enough for <paramref name="minimumLength"/>, a buffer will be rented
    ///  from the shared <see cref="ArrayPool{T}"/>.
    /// </param>
    /// <param name="minimumLength">
    ///  The required minimum length. If the <paramref name="initialBuffer"/> is not large enough, this will rent from
    ///  the shared <see cref="ArrayPool{T}"/>.
    /// </param>
    public BufferScope(Span<T> initialBuffer, int minimumLength)
    {
        if (initialBuffer.Length >= minimumLength)
        {
            _array = null;
            _span = initialBuffer;
        }
        else
        {
            _array = ArrayPool<T>.Shared.Rent(minimumLength);
            _span = _array;
        }
    }

    /// <summary>
    ///  Ensure that the buffer has enough space for <paramref name="capacity"/> number of elements.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Consider if creating new <see cref="BufferScope{T}"/> instances is possible and cleaner than using
    ///   this method.
    ///  </para>
    /// </remarks>
    /// <param name="capacity">The minimum number of elements the buffer should be able to hold.</param>
    /// <param name="copy">True to copy the existing elements when new space is allocated.</param>
    public void EnsureCapacity(int capacity, bool copy = false)
    {
        if (_span.Length >= capacity)
        {
            return;
        }

        // Keep method separate for better inlining.
        IncreaseCapacity(capacity, copy);
    }

    private void IncreaseCapacity(int capacity, bool copy)
    {
        Debug.Assert(capacity > _span.Length);

        T[] newArray = ArrayPool<T>.Shared.Rent(capacity);
        if (copy)
        {
            _span.CopyTo(newArray);
        }

        if (_array is not null)
        {
            ArrayPool<T>.Shared.Return(_array, clearArray: TypeInfo<T>.IsReferenceOrContainsReferences());
        }

        _array = newArray;
        _span = _array;
    }

    /// <summary>
    ///  Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="i">The zero-based index of the element to get or set.</param>
    /// <returns>The element at the specified index.</returns>
    public ref T this[int i]
        => ref _span[i];

    /// <summary>
    ///  Forms a slice out of the buffer starting at a specified index for a specified length.
    /// </summary>
    /// <param name="start">The index at which to begin the slice.</param>
    /// <param name="length">The desired length of the slice.</param>
    /// <returns>A span that consists of <paramref name="length"/> elements from the buffer starting at <paramref name="start"/>.</returns>
    public readonly Span<T> Slice(int start, int length)
        => _span.Slice(start, length);

    /// <inheritdoc cref="Span{T}.GetPinnableReference"/>
    /// <remarks>
    ///  This is used by C# to enable using the buffer in a fixed statement.
    /// </remarks>
    public readonly ref T GetPinnableReference()
        => ref _span.GetPinnableReference();

    /// <summary>
    ///  Gets the number of elements in the buffer.
    /// </summary>
    public readonly int Length => _span.Length;

    /// <summary>
    ///  Returns the buffer as a <see cref="Span{T}"/>.
    /// </summary>
    /// <returns>A span that represents the buffer.</returns>
    public readonly Span<T> AsSpan()
        => _span;

    /// <summary>
    ///  Implicitly converts a <see cref="BufferScope{T}"/> to a <see cref="Span{T}"/>.
    /// </summary>
    /// <param name="scope">The buffer scope to convert.</param>
    /// <returns>A span that represents the buffer.</returns>
    public static implicit operator Span<T>(BufferScope<T> scope)
        => scope._span;

    /// <summary>
    ///  Implicitly converts a <see cref="BufferScope{T}"/> to a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <param name="scope">The buffer scope to convert.</param>
    /// <returns>A read-only span that represents the buffer.</returns>
    public static implicit operator ReadOnlySpan<T>(BufferScope<T> scope)
        => scope._span;

    /// <summary>
    ///  Returns an enumerator for the buffer's backing <see cref="Span{T}"/>.
    /// </summary>
    public readonly Span<T>.Enumerator GetEnumerator()
        => _span.GetEnumerator();

    /// <summary>
    ///  Releases the rented array back to the <see cref="ArrayPool{T}"/> if one was rented.
    /// </summary>
    public void Dispose()
    {
        // Clear the span to avoid accidental use after returning the array.
        _span = default;

        if (_array is not null)
        {
            ArrayPool<T>.Shared.Return(_array, clearArray: TypeInfo<T>.IsReferenceOrContainsReferences());
        }

        _array = null;
    }

    /// <summary>
    ///  Returns a string representation of the buffer.
    /// </summary>
    /// <returns>A string representation of the buffer.</returns>
    public override readonly string ToString()
        => _span.ToString();
}
