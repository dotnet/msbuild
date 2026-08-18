// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Adapted from JeremyKuhne/touki:touki/Touki/Text/ValueStringBuilder.cs
//   https://github.com/JeremyKuhne/touki/blob/main/touki/Touki/Text/ValueStringBuilder.cs
// Original source: .NET Runtime and Windows Forms source code (MIT-licensed).
//
// Adaptations for MSBuild:
//   - Moved into the Microsoft.Build.Utilities namespace.
//   - Removed all [InterpolatedStringHandler]-related members
//     (the handler attribute, the (literalLength, formattedCount) constructors,
//     the format-provider fields, AppendLiteral, AppendFormatted, etc.).
//   - Removed Touki-specific dependencies (StringSegment, CopyTo overloads
//     that use .NET-only TextWriter / StringBuilder APIs).
//   - Polyfilled Span<char>.Replace for net472 with a manual loop.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Utilities;

/// <summary>
///  String builder struct that can be used to build strings in a memory-efficient way.
///  Allows using stack space for small strings.
/// </summary>
internal ref struct ValueStringBuilder
{
    // We're using a byte array here so the same backing buffer can be passed to APIs that take byte[] without
    // type-safety juggling. Byte arrays are always allocated on pointer-size boundaries so they're safe for
    // reinterpreting as Span<char>.
    private byte[]? _arrayToReturnToPool;
    private Span<char> _chars;
    private int _length;

    /// <summary>
    ///  Initializes a new instance of the <see cref="ValueStringBuilder"/> struct with an initial buffer.
    /// </summary>
    /// <param name="initialBuffer">The initial buffer to use for the string builder.</param>
    public ValueStringBuilder(Span<char> initialBuffer)
    {
        _arrayToReturnToPool = null;
        _chars = initialBuffer;
        _length = 0;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="ValueStringBuilder"/> struct with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity for the string builder.</param>
    public ValueStringBuilder(int initialCapacity)
    {
        _arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(initialCapacity * sizeof(char));
        _chars = MemoryMarshal.Cast<byte, char>((Span<byte>)_arrayToReturnToPool);
        _length = 0;
    }

    /// <summary>
    ///  Gets or sets the current length of the string builder.
    /// </summary>
    public int Length
    {
        readonly get => _length;
        set
        {
            // Do not free if set to 0. This allows in-place building of strings.
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, _chars.Length);
            _length = value;
        }
    }

    /// <summary>
    ///  Gets the maximum number of characters that can be contained in the memory allocated by the current instance.
    /// </summary>
    public readonly int Capacity => _chars.Length;

    /// <summary>
    ///  Clears the contents of the string builder, resetting its length to zero.
    /// </summary>
    public void Clear()
    {
        // Reset the position to 0, but do not clear the underlying array to allow in-place building.
        _length = 0;
    }

    /// <summary>
    ///  Ensures that the capacity of this builder is at least the specified value.
    /// </summary>
    public void EnsureCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        if ((uint)capacity > (uint)_chars.Length)
        {
            Grow(capacity - _length);
        }
    }

    /// <summary>
    ///  Get a pinnable reference to the builder. Always ensures that there is a trailing null.
    /// </summary>
    /// <remarks>
    ///  This overload is pattern matched in the C# 7.3+ compiler so you can omit
    ///  the explicit method call, and write e.g. <c>fixed (char* c = builder)</c>.
    /// </remarks>
    public ref char GetPinnableReference()
    {
        EnsureCapacity(_length + 1);
        _chars[_length] = '\0';
        return ref _chars.GetPinnableReference();
    }

    /// <summary>
    ///  Gets a reference to the character at the specified index.
    /// </summary>
    public ref char this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _length);
            return ref _chars[index];
        }
    }

    /// <summary>
    ///  Converts the value of this builder to a <see cref="string"/>.
    /// </summary>
    public override readonly string ToString() => _chars[.._length].ToString();

    /// <summary>
    ///  Converts the value of this builder to a <see cref="string"/> and disposes the builder.
    /// </summary>
    public string ToStringAndDispose()
    {
        string s = ToString();
        Dispose();
        return s;
    }

    /// <summary>
    ///  Returns a span around the contents of the builder.
    /// </summary>
    /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/>.</param>
    public ReadOnlySpan<char> AsSpan(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            _chars[Length] = '\0';
        }

        return _chars[.._length];
    }

    /// <summary>
    ///  Returns a read-only span around the contents of the builder.
    /// </summary>
    public readonly ReadOnlySpan<char> AsSpan() => _chars[.._length];

    /// <summary>
    ///  Gets a slice of the builder using the specified range.
    /// </summary>
    public readonly ReadOnlySpan<char> this[Range range] => _chars[.._length][range];

    /// <summary>
    ///  Forms a slice out of the buffer starting at a specified index.
    /// </summary>
    public readonly ReadOnlySpan<char> Slice(int start)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(start, _length);
        return _chars[start.._length];
    }

    /// <summary>
    ///  Forms a slice out of the buffer starting at a specified index for a specified length.
    /// </summary>
    public readonly ReadOnlySpan<char> Slice(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(start, _length);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start + length, _length);

        return _chars.Slice(start, length);
    }

    /// <summary>
    ///  Returns a span of the requested length at the current position in the builder that can be
    ///  used to directly append characters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> AppendSpan(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        int originalLength = _length;
        if (originalLength > _chars.Length - length)
        {
            Grow(length);
        }

        _length = originalLength + length;
        return _chars.Slice(originalLength, length);
    }

    /// <summary>
    ///  Attempts to copy the contents of this builder to the destination span.
    /// </summary>
    public readonly bool TryCopyTo(Span<char> destination, out int charsWritten)
    {
        if (_chars[.._length].TryCopyTo(destination))
        {
            charsWritten = _length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    /// <summary>
    ///  Inserts the specified character a specified number of times at the specified index.
    /// </summary>
    public void Insert(int index, char value, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _length);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_length > _chars.Length - count)
        {
            Grow(count);
        }

        int remaining = _length - index;
        _chars.Slice(index, remaining).CopyTo(_chars[(index + count)..]);
        _chars.Slice(index, count).Fill(value);
        _length += count;
    }

    /// <summary>
    ///  Inserts the specified string at the specified index.
    /// </summary>
    public void Insert(int index, string? s)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _length);

        if (s is null)
        {
            return;
        }

        int count = s.Length;

        if (_length > _chars.Length - count)
        {
            Grow(count);
        }

        int remaining = _length - index;
        _chars.Slice(index, remaining).CopyTo(_chars[(index + count)..]);
        s.AsSpan().CopyTo(_chars[index..]);
        _length += count;
    }

    /// <summary>
    ///  Appends the specified character to the end of this builder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c)
    {
        int pos = _length;
        if ((uint)pos < (uint)_chars.Length)
        {
            _chars[pos] = c;
            _length = pos + 1;
        }
        else
        {
            GrowAndAppend(c);
        }
    }

    /// <summary>
    ///  Appends a string to the builder. If the string is <see langword="null"/>, this method does nothing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string? s)
    {
        if (s is null)
        {
            return;
        }

        int pos = _length;
        if (s.Length == 1 && (uint)pos < (uint)_chars.Length)
        {
            // Very common case, e.g. appending separators or single-char delimiters.
            _chars[pos] = s[0];
            _length = pos + 1;
        }
        else
        {
            AppendSlow(s);
        }
    }

    private void AppendSlow(string s)
    {
        int pos = _length;
        if (pos > _chars.Length - s.Length)
        {
            Grow(s.Length);
        }

        s.AsSpan().CopyTo(_chars[pos..]);
        _length += s.Length;
    }

    /// <summary>
    ///  Appends the specified character a specified number of times to the end of this builder.
    /// </summary>
    public void Append(char c, int count)
    {
        if (_length > _chars.Length - count)
        {
            Grow(count);
        }

        Span<char> dst = _chars.Slice(_length, count);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = c;
        }

        _length += count;
    }

    /// <summary>
    ///  Appends characters from a pointer to the end of this builder.
    /// </summary>
    public unsafe void Append(char* value, int length)
    {
        int pos = _length;
        if (pos > _chars.Length - length)
        {
            Grow(length);
        }

        Span<char> dst = _chars.Slice(_length, length);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = *value++;
        }

        _length += length;
    }

    /// <summary>
    ///  Appends the specified read-only span of characters to the end of this builder.
    /// </summary>
    public void Append(scoped ReadOnlySpan<char> value)
    {
        int pos = _length;
        if (pos > _chars.Length - value.Length)
        {
            Grow(value.Length);
        }

        value.CopyTo(_chars[_length..]);
        _length += value.Length;
    }

    /// <summary>
    ///  Replace all occurrences of a character in the builder with another character.
    /// </summary>
    public readonly void Replace(char oldValue, char newValue)
    {
        if (_length == 0 || oldValue == newValue)
        {
            return;
        }

        _chars[.._length].Replace(oldValue, newValue);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(char c)
    {
        Grow(1);
        Append(c);
    }

    /// <summary>
    ///  Resize the internal buffer either by doubling current buffer size or by adding
    ///  <paramref name="additionalCapacityBeyondPos"/> to <see cref="_length"/> whichever is greater.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [MemberNotNull(nameof(_arrayToReturnToPool))]
    private void Grow(int additionalCapacityBeyondPos)
    {
        Debug.Assert(additionalCapacityBeyondPos > 0);
        Debug.Assert(_length > _chars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

        const uint ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength

        // Increase to at least the required size, but try to double the size if possible,
        // bounding the doubling to not go beyond the max array length.
        int newCapacity = (int)Math.Max(
            (uint)(_length + additionalCapacityBeyondPos),
            Math.Min((uint)_chars.Length * 2, ArrayMaxLength));

        // Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative.
        byte[] poolArray = ArrayPool<byte>.Shared.Rent(newCapacity * sizeof(char));

        _chars[.._length].CopyTo(MemoryMarshal.Cast<byte, char>((Span<byte>)poolArray));

        byte[]? toReturn = _arrayToReturnToPool;
        _chars = MemoryMarshal.Cast<byte, char>((Span<byte>)poolArray);
        _arrayToReturnToPool = poolArray;
        if (toReturn is not null)
        {
            ArrayPool<byte>.Shared.Return(toReturn);
        }
    }

    /// <summary>
    ///  Releases all resources used by the <see cref="ValueStringBuilder"/>.
    /// </summary>
    /// <remarks>
    ///  Returns any rented array back to the array pool and resets the builder to its default state.
    ///  After calling this method the builder should not be used again.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        byte[]? toReturn = _arrayToReturnToPool;

        // Clear the fields to prevent accidental reuse.
        _arrayToReturnToPool = null;
        _chars = default;
        _length = 0;

        if (toReturn is not null)
        {
            ArrayPool<byte>.Shared.Return(toReturn);
        }
    }

    /// <summary>
    ///  Implicitly converts a <see cref="ValueStringBuilder"/> to a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public static implicit operator ReadOnlySpan<char>(ValueStringBuilder builder) => builder._chars[..builder._length];
}
