// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This code is adapted from https://github.com/dotnet/runtime/blob/284c0ae38e3eac0f6ad5cdaa0156d22fc6fc3915/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/ValueListBuilder.cs.

using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Collections;

/// <summary>
///  A ref struct builder for arrays that uses pooled memory for efficient allocation.
///  This builder automatically grows as needed and returns memory to the pool when disposed.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
internal ref struct RefArrayBuilder<T>
{
    private BufferScope<T> _scope;
    private int _count;

    /// <summary>
    ///  Initializes a new instance of the <see cref="RefArrayBuilder{T}"/> struct with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the builder.</param>
    public RefArrayBuilder(int initialCapacity)
    {
        _scope = new BufferScope<T>(initialCapacity);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RefArrayBuilder{T}"/> struct with the specified scratch buffer.
    /// </summary>
    /// <param name="scratchBuffer">The initial buffer to use for storing elements.</param>
    /// <remarks>
    ///  <paramref name="scratchBuffer"/> should generally be stack-allocated to avoid heap allocations.
    /// </remarks>
    public RefArrayBuilder(Span<T> scratchBuffer)
    {
        _scope = new BufferScope<T>(scratchBuffer);
    }

    /// <summary>
    ///  Releases the pooled array back to the shared <see cref="ArrayPool{T}"/>.
    ///  This method can be called multiple times safely.
    /// </summary>
    public void Dispose()
    {
        _scope.Dispose();
    }

    /// <summary>
    ///  Gets the current capacity of the builder.
    /// </summary>
    public readonly int Capacity => _scope.Length;

    /// <summary>
    ///  Gets a value indicating whether the builder contains no elements.
    /// </summary>
    /// <value>
    ///  <see langword="true"/> if the builder contains no elements; otherwise, <see langword="false"/>.
    /// </value>
    public readonly bool IsEmpty => _count == 0;

    /// <summary>
    ///  Gets or sets the number of elements in the builder.
    /// </summary>
    public int Count
    {
        readonly get => _count;
        set
        {
            Debug.Assert(value >= 0, "Count must be non-negative.");
            Debug.Assert(value <= _scope.Length, "Count must not exceed the span length.");

            _count = value;
        }
    }

    /// <summary>
    ///  Gets a reference to the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get.</param>
    /// <returns>A reference to the element at the specified index.</returns>
    public ref T this[int index]
    {
        get
        {
            Debug.Assert(index < _count, "Index must be less than Count.");

            return ref _scope[index];
        }
    }

    /// <summary>
    ///  Returns a reference to this builder, allowing it to be passed by ref
    ///  even when declared in a <see langword="using"/> statement.
    /// </summary>
    [UnscopedRef]
    public ref RefArrayBuilder<T> AsRef() => ref this;

    /// <summary>
    ///  Returns a <see cref="ReadOnlySpan{T}"/> view of the elements in the builder.
    /// </summary>
    /// <returns>A read-only span view of the elements.</returns>
    public readonly Span<T> AsSpan()
        => _scope.AsSpan()[.._count];

    /// <summary>
    ///  Adds an item to the end of the builder. The builder will automatically grow if needed.
    /// </summary>
    /// <param name="item">The item to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        int count = _count;
        Span<T> span = _scope;

        if ((uint)count < (uint)span.Length)
        {
            span[count] = item;
            _count = count + 1;
        }
        else
        {
            AddWithResize(item);
        }
    }

    // Hide uncommon path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithResize(T item)
    {
        Debug.Assert(_count == _scope.Length, "AddWithResize should only be called when the span is full.");

        int count = _count;

        Grow(1);

        _scope[count] = item;
        _count = count + 1;
    }

    /// <summary>
    ///  Adds a range of elements to the end of the builder. The builder will automatically grow if needed.
    /// </summary>
    /// <param name="source">The span of elements to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(scoped ReadOnlySpan<T> source)
    {
        int count = _count;
        Span<T> span = _scope;

        if (source.Length == 1 && (uint)count < (uint)span.Length)
        {
            span[count] = source[0];
            _count = count + 1;
        }
        else
        {
            AddRangeCore(source);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddRangeCore(scoped ReadOnlySpan<T> source)
    {
        int count = _count;
        Span<T> span = _scope;

        if ((uint)(count + source.Length) > (uint)span.Length)
        {
            Grow(size: source.Length);

            // Reset span since we grew.
            span = _scope;
        }

        source.CopyTo(span.Slice(start: count));
        _count = count + source.Length;
    }

    /// <summary>
    ///  Inserts an item at the specified index, shifting subsequent elements. The builder will automatically grow if needed.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the item.</param>
    /// <param name="item">The item to insert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(int index, T item)
    {
        Debug.Assert(index >= 0, "Insert index must be non-negative.");
        Debug.Assert(index <= _count, "Insert index must not exceed Count.");

        int count = _count;
        Span<T> span = _scope;

        if ((uint)count < (uint)span.Length)
        {
            // Shift existing items
            int toCopy = count - index;
            span.Slice(index, toCopy).CopyTo(span.Slice(index + 1, toCopy));

            span[index] = item;
            _count = count + 1;
        }
        else
        {
            InsertWithResize(index, item);
        }
    }

    // Hide uncommon path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InsertWithResize(int index, T item)
    {
        Debug.Assert(_count == _scope.Length, "InsertWithResize should only be called when the span is full.");

        Grow(size: 1, startIndex: index);

        _scope[index] = item;
        _count += 1;
    }

    /// <summary>
    ///  Inserts a range of elements at the specified index, shifting subsequent elements.
    ///  The builder will automatically grow if needed.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the elements.</param>
    /// <param name="source">The span of elements to insert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InsertRange(int index, scoped ReadOnlySpan<T> source)
    {
        Debug.Assert(index >= 0, "Insert index must be non-negative.");
        Debug.Assert(index <= _count, "Insert index must not exceed Count.");

        int count = _count;
        Span<T> span = _scope;

        if ((uint)(count + source.Length) <= (uint)span.Length)
        {
            // Shift existing items
            int toCopy = count - index;
            span.Slice(index, toCopy).CopyTo(span.Slice(index + source.Length, toCopy));

            source.CopyTo(span.Slice(index));
            _count = count + source.Length;
        }
        else
        {
            InsertRangeCore(index, source);
        }
    }

    // Hide uncommon path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InsertRangeCore(int index, scoped ReadOnlySpan<T> source)
    {
        int count = _count;
        Span<T> span = _scope;

        if ((uint)(count + source.Length) > (uint)span.Length)
        {
            Grow(size: source.Length, startIndex: index);

            // Reset span since we grew.
            span = _scope;
        }

        source.CopyTo(span.Slice(index, source.Length));
        _count = count + source.Length;
    }

    private void Grow(int size = 1, int startIndex = -1)
    {
        Debug.Assert(startIndex >= -1, "Start index must be -1 or non-negative.");
        Debug.Assert(startIndex <= _count, "Start index must not exceed Count.");

        const int ArrayMaxLength = 0x7FFFFFC7; // Same as Array.MaxLength;

        Span<T> span = _scope;

        // Double the size of the span.  If it's currently empty, default to size 4,
        // although it'll be increased in Rent to the pool's minimum bucket size.
        int nextCapacity = Math.Max(
            val1: span.Length != 0 ? span.Length * 2 : 4,
            val2: span.Length + size);

        // If the computed doubled capacity exceeds the possible length of an array, then we
        // want to downgrade to either the maximum array length if that's large enough to hold
        // an additional item, or the current length + 1 if it's larger than the max length, in
        // which case it'll result in an OOM when calling Rent below.  In the exceedingly rare
        // case where _span.Length is already int.MaxValue (in which case it couldn't be a managed
        // array), just use that same value again and let it OOM in Rent as well.
        if ((uint)nextCapacity > ArrayMaxLength)
        {
            nextCapacity = Math.Max(Math.Max(span.Length + 1, ArrayMaxLength), span.Length);
        }

        if (startIndex == -1)
        {
            _scope.EnsureCapacity(nextCapacity, copy: true);
        }
        else
        {
            // Need to manually copy to new buffer to make room for inserted items
            // at startIndex. The EnsureCapacity(copy: true) path won't work here
            // because it always copies starting at index 0. So, we create a new
            // buffer and copy the segments before and after startIndex separately.
            var newScope = new BufferScope<T>(nextCapacity);

            Span<T> destination = newScope.AsSpan();

            if (startIndex > 0)
            {
                span[..startIndex].CopyTo(destination);
            }

            span[startIndex.._count].CopyTo(destination.Slice(startIndex + size));

            _scope.Dispose();
            _scope = newScope;
        }
    }

    /// <summary>
    ///  Removes the element at the specified index, shifting subsequent elements.
    /// </summary>
    /// <param name="index">The zero-based index of the element to remove.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveAt(int index)
    {
        Debug.Assert(index >= 0, "Remove index must be non-negative.");
        Debug.Assert(index < _count, "Remove index must be less than Count.");

        int count = _count;
        Span<T> span = _scope;

        // Shift subsequent elements down by one
        int toCopy = count - index - 1;
        if (toCopy > 0)
        {
            span.Slice(index + 1, toCopy).CopyTo(span.Slice(index, toCopy));
        }

        // Clear the last element if it contains references
        if (TypeInfo<T>.IsReferenceOrContainsReferences())
        {
            span[count - 1] = default!;
        }

        _count = count - 1;
    }

    /// <summary>
    ///  Creates an <see cref="ImmutableArray{T}"/> containing a copy of the elements in the builder.
    /// </summary>
    /// <returns>An immutable array containing the elements.</returns>
    public readonly ImmutableArray<T> ToImmutable()
        => ImmutableArray.Create(AsSpan());

    /// <summary>
    ///  Determines whether the builder contains any elements.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the builder contains any elements; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Any()
        => !IsEmpty;

    /// <summary>
    ///  Determines whether any element in the builder satisfies a condition.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>
    ///  <see langword="true"/> if any element satisfies the condition; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Any(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        foreach (T item in AsSpan())
        {
            if (predicate(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Determines whether any element in the builder satisfies a condition using an additional argument.
    /// </summary>
    /// <typeparam name="TArg">The type of the additional argument.</typeparam>
    /// <param name="arg">The additional argument to pass to the predicate.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>
    ///  <see langword="true"/> if any element satisfies the condition; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Any<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        foreach (T item in AsSpan())
        {
            if (predicate(item, arg))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Determines whether all elements in the builder satisfy a condition.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>
    ///  <see langword="true"/> if all elements satisfy the condition; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool All(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        foreach (T item in AsSpan())
        {
            if (!predicate(item))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Determines whether all elements in the builder satisfy a condition using an additional argument.
    /// </summary>
    /// <typeparam name="TArg">The type of the additional argument.</typeparam>
    /// <param name="arg">The additional argument to pass to the predicate.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>
    ///  <see langword="true"/> if all elements satisfy the condition; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool All<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        foreach (T item in AsSpan())
        {
            if (!predicate(item, arg))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Returns the first element in the builder.
    /// </summary>
    /// <returns>The first element in the builder.</returns>
    /// <exception cref="InvalidOperationException">The builder is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T First()
    {
        T? first = TryGetFirst(out bool found);

        if (!found)
        {
            ThrowInvalidOperation(SR.Format_0_contains_no_elements(nameof(RefArrayBuilder<>)));
        }

        return first!;
    }

    /// <summary>
    ///  Returns the first element in the builder that satisfies a condition.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>The first element that satisfies the condition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No element satisfies the condition.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T First(Func<T, bool> predicate)
    {
        T? first = TryGetFirst(predicate, out bool found);

        if (!found)
        {
            ThrowInvalidOperation(SR.Format_0_does_not_contain_matching_element(nameof(RefArrayBuilder<>)));
        }

        return first!;
    }

    /// <summary>
    ///  Returns the first element in the builder that satisfies a condition using an additional argument.
    /// </summary>
    /// <typeparam name="TArg">The type of the additional argument.</typeparam>
    /// <param name="arg">The additional argument to pass to the predicate.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>The first element that satisfies the condition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No element satisfies the condition.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T First<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        T? first = TryGetFirst(predicate, arg, out bool found);

        if (!found)
        {
            ThrowInvalidOperation(SR.Format_0_does_not_contain_matching_element(nameof(RefArrayBuilder<>)));
        }

        return first!;
    }

    /// <summary>
    ///  Returns the first element in the builder, or a default value if the builder is empty.
    /// </summary>
    /// <returns>The first element in the builder, or <see langword="default"/>(<typeparamref name="T"/>) if the builder is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T? FirstOrDefault()
        => TryGetFirst(out _);

    /// <summary>
    ///  Returns the first element in the builder, or a specified default value if the builder is empty.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the builder is empty.</param>
    /// <returns>
    ///  The first element in the builder, or <paramref name="defaultValue"/> if the builder is empty.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T FirstOrDefault(T defaultValue)
    {
        T? first = TryGetFirst(out bool found);
        return found ? first! : defaultValue;
    }

    /// <summary>
    ///  Returns the first element that satisfies a condition, or a default value if no such element is found.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>
    ///  The first element that satisfies the condition, or <see langword="default"/>(<typeparamref name="T"/>) if no such element is found.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T? FirstOrDefault(Func<T, bool> predicate)
        => TryGetFirst(predicate, out _);

    /// <summary>
    ///  Returns the first element that satisfies a condition, or a specified default value if no such element is found.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="defaultValue">The default value to return if no element satisfies the condition.</param>
    /// <returns>
    ///  The first element that satisfies the condition, or <paramref name="defaultValue"/> if no such element is found.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T FirstOrDefault(Func<T, bool> predicate, T defaultValue)
    {
        T? first = TryGetFirst(predicate, out bool found);
        return found ? first! : defaultValue;
    }

    /// <summary>
    ///  Returns the first element that satisfies a condition using an additional argument, or a default value if no such element is found.
    /// </summary>
    /// <typeparam name="TArg">The type of the additional argument.</typeparam>
    /// <param name="arg">The additional argument to pass to the predicate.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>
    ///  The first element that satisfies the condition, or <see langword="default"/>(<typeparamref name="T"/>) if no such element is found.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T? FirstOrDefault<TArg>(TArg arg, Func<T, TArg, bool> predicate)
        => TryGetFirst(predicate, arg, out _);

    /// <summary>
    ///  Returns the first element that satisfies a condition using an additional argument, or a specified default value if no such element is found.
    /// </summary>
    /// <typeparam name="TArg">The type of the additional argument.</typeparam>
    /// <param name="arg">The additional argument to pass to the predicate.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="defaultValue">The default value to return if no element satisfies the condition.</param>
    /// <returns>
    ///  The first element that satisfies the condition, or <paramref name="defaultValue"/> if no such element is found.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T FirstOrDefault<TArg>(TArg arg, Func<T, TArg, bool> predicate, T defaultValue)
    {
        T? first = TryGetFirst(predicate, arg, out bool found);
        return found ? first! : defaultValue;
    }

    /// <summary>
    ///  Returns the last element in the builder.
    /// </summary>
    /// <returns>The last element in the builder.</returns>
    /// <exception cref="InvalidOperationException">The builder is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T Last()
    {
        T? last = TryGetLast(out bool found);

        if (!found)
        {
            ThrowInvalidOperation(SR.Format_0_contains_no_elements(nameof(RefArrayBuilder<>)));
        }

        return last!;
    }

    /// <summary>
    ///  Returns the last element in the builder that satisfies a condition.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>The last element that satisfies the condition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No element satisfies the condition.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T Last(Func<T, bool> predicate)
    {
        T? last = TryGetLast(predicate, out bool found);

        if (!found)
        {
            ThrowInvalidOperation(SR.Format_0_does_not_contain_matching_element(nameof(RefArrayBuilder<>)));
        }

        return last!;
    }

    /// <summary>
    ///  Returns the last element in the builder that satisfies a condition using an additional argument.
    /// </summary>
    /// <typeparam name="TArg">The type of the additional argument.</typeparam>
    /// <param name="arg">The additional argument to pass to the predicate.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>The last element that satisfies the condition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No element satisfies the condition.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T Last<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        T? last = TryGetLast(predicate, arg, out bool found);

        if (!found)
        {
            ThrowInvalidOperation(SR.Format_0_does_not_contain_matching_element(nameof(RefArrayBuilder<>)));
        }

        return last!;
    }

    /// <summary>
    ///  Returns the last element in the builder, or a default value if the builder is empty.
    /// </summary>
    /// <returns>The last element in the builder, or <see langword="default"/>(<typeparamref name="T"/>) if the builder is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T? LastOrDefault()
        => TryGetLast(out _);

    /// <summary>
    ///  Returns the last element in the builder, or a specified default value if the builder is empty.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the builder is empty.</param>
    /// <returns>
    ///  The last element in the builder, or <paramref name="defaultValue"/> if the builder is empty.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T LastOrDefault(T defaultValue)
    {
        T? last = TryGetLast(out bool found);
        return found ? last! : defaultValue;
    }

    /// <summary>
    ///  Returns the last element that satisfies a condition, or a default value if no such element is found.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>
    ///  The last element that satisfies the condition, or <see langword="default"/>(<typeparamref name="T"/>) if no such element is found.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T? LastOrDefault(Func<T, bool> predicate)
        => TryGetLast(predicate, out _);

    /// <summary>
    ///  Returns the last element that satisfies a condition, or a specified default value if no such element is found.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="defaultValue">The default value to return if no element satisfies the condition.</param>
    /// <returns>
    ///  The last element that satisfies the condition, or <paramref name="defaultValue"/> if no such element is found.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T LastOrDefault(Func<T, bool> predicate, T defaultValue)
    {
        T? last = TryGetLast(predicate, out bool found);
        return found ? last! : defaultValue;
    }

    /// <summary>
    ///  Returns the last element that satisfies a condition using an additional argument, or a default value if no such element is found.
    /// </summary>
    /// <typeparam name="TArg">The type of the additional argument.</typeparam>
    /// <param name="arg">The additional argument to pass to the predicate.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <returns>
    ///  The last element that satisfies the condition, or <see langword="default"/>(<typeparamref name="T"/>) if no such element is found.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T? LastOrDefault<TArg>(TArg arg, Func<T, TArg, bool> predicate)
        => TryGetLast(predicate, arg, out _);

    /// <summary>
    ///  Returns the last element that satisfies a condition using an additional argument, or a specified default value if no such element is found.
    /// </summary>
    /// <typeparam name="TArg">The type of the additional argument.</typeparam>
    /// <param name="arg">The additional argument to pass to the predicate.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="defaultValue">The default value to return if no element satisfies the condition.</param>
    /// <returns>
    ///  The last element that satisfies the condition, or <paramref name="defaultValue"/> if no such element is found.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T LastOrDefault<TArg>(TArg arg, Func<T, TArg, bool> predicate, T defaultValue)
    {
        T? last = TryGetLast(predicate, arg, out bool found);
        return found ? last! : defaultValue;
    }

    private readonly T? TryGetFirst(out bool found)
    {
        if (!IsEmpty)
        {
            found = true;
            return _scope[0];
        }

        found = false;
        return default;
    }

    private readonly T? TryGetFirst(Func<T, bool> predicate, out bool found)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        foreach (T item in AsSpan())
        {
            if (predicate(item))
            {
                found = true;
                return item;
            }
        }

        found = false;
        return default;
    }

    private readonly T? TryGetFirst<TArg>(Func<T, TArg, bool> predicate, TArg arg, out bool found)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        foreach (T item in AsSpan())
        {
            if (predicate(item, arg))
            {
                found = true;
                return item;
            }
        }

        found = false;
        return default;
    }

    private readonly T? TryGetLast(out bool found)
    {
        if (!IsEmpty)
        {
            found = true;
            return _scope[_count - 1];
        }

        found = false;
        return default;
    }

    private readonly T? TryGetLast(Func<T, bool> predicate, out bool found)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        for (int i = _count - 1; i >= 0; i--)
        {
            T item = _scope[i];
            if (predicate(item))
            {
                found = true;
                return item;
            }
        }

        found = false;
        return default;
    }

    private readonly T? TryGetLast<TArg>(Func<T, TArg, bool> predicate, TArg arg, out bool found)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        for (int i = _count - 1; i >= 0; i--)
        {
            T item = _scope[i];
            if (predicate(item, arg))
            {
                found = true;
                return item;
            }
        }

        found = false;
        return default;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    private static void ThrowInvalidOperation(string message)
        => throw new InvalidOperationException(message);
}
