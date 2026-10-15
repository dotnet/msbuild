// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Collections;

/// <summary>
///  A last-in, first-out stack that uses caller-supplied or pooled storage and grows as needed.
/// </summary>
/// <typeparam name="T">The type of elements in the stack.</typeparam>
/// <remarks>
///  A default-initialized instance is an empty stack and rents storage on the first push.
///  Dispose the stack to return rented storage to the pool. Do not copy an active stack:
///  copies share storage and cannot be mutated or disposed independently.
/// </remarks>
internal ref struct RefStack<T>
{
    private RefArrayBuilder<T> _builder;

    /// <summary>
    ///  Gets a value indicating whether the stack is empty.
    /// </summary>
    /// <value>
    ///  <see langword="true"/> if the stack contains no items; otherwise, <see langword="false"/>.
    /// </value>
    public readonly bool IsEmpty => _builder.IsEmpty;

    /// <summary>
    ///  Initializes an empty stack with rented storage of at least the specified capacity.
    /// </summary>
    /// <param name="initialCapacity">The minimum initial capacity of the stack.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is negative.</exception>
    public RefStack(int initialCapacity)
    {
        _builder = new RefArrayBuilder<T>(initialCapacity);
    }

    /// <summary>
    ///  Initializes an empty stack using the specified scratch buffer.
    /// </summary>
    /// <param name="scratchBuffer">The initial buffer to use for storing elements.</param>
    /// <remarks>
    ///  The stack uses <paramref name="scratchBuffer"/> until it needs more capacity, then rents a larger buffer.
    ///  The supplied buffer is not returned to the pool. For unmanaged element types, it can be stack-allocated
    ///  to avoid an initial heap allocation.
    /// </remarks>
    public RefStack(Span<T> scratchBuffer)
    {
        _builder = new RefArrayBuilder<T>(scratchBuffer);
    }

    /// <summary>
    ///  Returns any rented storage to the shared <see cref="ArrayPool{T}"/>.
    /// </summary>
    /// <remarks>
    ///  Calling this method more than once is safe. Do not otherwise use the stack after disposal.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        _builder.Dispose();
    }

    /// <summary>
    ///  Adds an item to the top of the stack, growing its storage if needed.
    /// </summary>
    /// <param name="item">The item to push onto the stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(T item)
    {
        _builder.Add(item);
    }

    /// <summary>
    ///  Removes and returns the item at the top of the stack.
    /// </summary>
    /// <returns>
    ///  The item removed from the top of the stack.
    /// </returns>
    /// <exception cref="InvalidOperationException">The stack is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Pop()
    {
        if (IsEmpty)
        {
            InvalidOperationException.Throw(SR.Format_0_is_empty(nameof(RefStack<>)));
        }

        int lastIndex = _builder.Count - 1;
        T item = _builder[lastIndex];
        _builder.RemoveAt(lastIndex);

        return item;
    }

    /// <summary>
    ///  Attempts to remove and return the item at the top of the stack.
    /// </summary>
    /// <param name="item">
    ///  When this method returns, contains the removed item if the stack was not empty;
    ///  otherwise, <see langword="default"/>(<typeparamref name="T"/>).
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if an item was removed; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  An empty stack returns <see langword="false"/> rather than throwing an exception.
    ///  Removing a stored <see langword="null"/> or default value still returns <see langword="true"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop([MaybeNullWhen(false)] out T item)
    {
        if (!IsEmpty)
        {
            item = Pop();
            return true;
        }

        item = default;
        return false;
    }

    /// <summary>
    ///  Returns the item at the top of the stack without removing it.
    /// </summary>
    /// <returns>
    ///  The item at the top of the stack.
    /// </returns>
    /// <exception cref="InvalidOperationException">The stack is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T Peek()
    {
        if (IsEmpty)
        {
            InvalidOperationException.Throw(SR.Format_0_is_empty(nameof(RefStack<>)));
        }

        return _builder.Last();
    }

    /// <summary>
    ///  Returns the item at the top of the stack without removing it, or the default value if the stack is empty.
    /// </summary>
    /// <returns>
    ///  The item at the top of the stack, or <see langword="default"/>(<typeparamref name="T"/>) if the stack is empty.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T? PeekOrDefault()
        => _builder.LastOrDefault();

    /// <summary>
    ///  Returns the item at the top of the stack without removing it, or a specified default value if the stack is empty.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the stack is empty.</param>
    /// <returns>
    ///  The item at the top of the stack, or <paramref name="defaultValue"/> if the stack is empty.
    /// </returns>
    /// <remarks>
    ///  A stored <see langword="null"/> or default value is returned as-is, not replaced with <paramref name="defaultValue"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T PeekOrDefault(T defaultValue)
        => _builder.LastOrDefault(defaultValue);

    /// <summary>
    ///  Attempts to retrieve the item at the top of the stack without removing it.
    /// </summary>
    /// <param name="item">
    ///  When this method returns, contains the top item if the stack is not empty;
    ///  otherwise, <see langword="default"/>(<typeparamref name="T"/>).
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the stack is not empty; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  A stored <see langword="null"/> or default value still returns <see langword="true"/>.
    /// </remarks>
    public readonly bool TryPeek([MaybeNullWhen(false)] out T item)
    {
        if (!IsEmpty)
        {
            item = _builder.Last();
            return true;
        }

        item = default;
        return false;
    }
}
