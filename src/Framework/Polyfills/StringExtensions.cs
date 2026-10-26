// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build;

internal static class StringExtensions
{
    /// <inheritdoc cref="string.IsNullOrEmpty(string)"/>
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
        => string.IsNullOrEmpty(value);

    /// <inheritdoc cref="string.IsNullOrWhiteSpace(string)"/>
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value)
        => string.IsNullOrWhiteSpace(value);

    /// <summary>
    ///  Gets the bounds of a string after excluding leading and trailing whitespace.
    /// </summary>
    /// <param name="value">The string whose bounds are trimmed.</param>
    /// <param name="startIndex">The zero-based start index after excluding leading whitespace.</param>
    /// <param name="length">The length after excluding leading and trailing whitespace.</param>
    /// <returns>
    ///  <see langword="true"/> if the bounds exclude any whitespace; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public static bool GetTrimBounds(this string value, out int startIndex, out int length)
    {
        startIndex = 0;
        length = value.Length;
        return value.TrimBounds(ref startIndex, ref length);
    }

    /// <summary>
    ///  Trims leading and trailing whitespace from the bounds of a string segment.
    /// </summary>
    /// <param name="value">The string containing the segment.</param>
    /// <param name="startIndex">The zero-based start index, updated to exclude leading whitespace.</param>
    /// <param name="length">The segment length, updated to exclude leading and trailing whitespace.</param>
    /// <returns>
    ///  <see langword="true"/> if the segment bounds changed; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TrimBounds(this string value, ref int startIndex, ref int length)
    {
        int originalLength = length;
        int endIndex = startIndex + length;

        while (startIndex < endIndex && char.IsWhiteSpace(value[startIndex]))
        {
            startIndex++;
        }

        while (endIndex > startIndex && char.IsWhiteSpace(value[endIndex - 1]))
        {
            endIndex--;
        }

        length = endIndex - startIndex;
        return length != originalLength;
    }

    extension(string)
    {
        /// <summary>
        ///  Determines whether regions of two strings are equal using the specified comparison.
        /// </summary>
        /// <param name="strA">The first string to compare.</param>
        /// <param name="indexA">The zero-based starting position of the region within <paramref name="strA"/>.</param>
        /// <param name="strB">The second string to compare.</param>
        /// <param name="indexB">The zero-based starting position of the region within <paramref name="strB"/>.</param>
        /// <param name="length">The number of characters in each region to compare.</param>
        /// <param name="comparisonType">The string comparison rules to use.</param>
        /// <returns>
        ///  <see langword="true"/> if the specified regions are equal; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        public static bool Equals(string strA, int indexA, string strB, int indexB, int length, StringComparison comparisonType)
            => string.Compare(strA, indexA, strB, indexB, length, comparisonType) == 0;

        /// <summary>
        ///  Determines whether the portion of one string beginning at a specified position is
        ///  equal to another string using the specified comparison.
        /// </summary>
        /// <param name="strA">The string containing the region to compare.</param>
        /// <param name="indexA">The zero-based starting position of the region within <paramref name="strA"/>.</param>
        /// <param name="strB">The string to compare with the specified region.</param>
        /// <param name="comparisonType">The string comparison rules to use.</param>
        /// <returns>
        ///  <see langword="true"/> if the characters beginning at <paramref name="indexA"/> equal
        ///  <paramref name="strB"/>;
        ///  otherwise, <see langword="false"/>.
        /// </returns>
        public static bool Equals(string strA, int indexA, string strB, StringComparison comparisonType)
            => string.Compare(strA, indexA, strB, 0, strB.Length, comparisonType) == 0;

        /// <summary>
        ///  Allocates a string of the specified length filled with null characters.
        /// </summary>
        public static string FastAllocateString(int length)
            // This calls FastAllocateString in the runtime, with extra checks.
            => new('\0', length);

#if !NET
        /// <summary>
        ///  Creates a new string with a specific length and initializes it after creation by using
        ///  the specified callback.
        /// </summary>
        /// <typeparam name="TState">
        ///  The type of the element to pass to <paramref name="action"/>.
        /// </typeparam>
        /// <param name="length">The length of the string to create.</param>
        /// <param name="state">The element to pass to <paramref name="action"/>.</param>
        /// <param name="action">The callback to initialize the string.</param>
        /// <returns>
        ///  The created string.
        /// </returns>
        /// <remarks>
        /// <para>
        ///  The initial content of the destination span passed to <paramref name="action"/> is undefined.
        ///  Therefore, it is the delegate's responsibility to ensure that every element of the span is assigned.
        ///  Otherwise, the resulting string could contain random characters.
        /// </para>
        /// <para>
        ///  To support interop scenarios, the underlying buffer is guaranteed to be at least 1 greater than
        ///  represented by the span parameter of the action callback. This additional index represents the
        ///  null-terminator and, if written, that is the only value supported. Writing any value other than the
        ///  null-terminator corrupts the string and is considered undefined behavior.
        /// </para>
        /// </remarks>
        public static unsafe string Create<TState>(int length, TState state, SpanAction<char, TState> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentOutOfRangeException.ThrowIfNegative(length);

            if (length == 0)
            {
                return string.Empty;
            }

            string result = FastAllocateString(length);

            fixed (char* p = result)
            {
                action(new Span<char>(p, length), state);
            }

            return result;
        }
#endif
    }
}

#if !NET
/// <summary>
///  Encapsulates a method that receives a span of objects of type <typeparamref name="T"/> and a state 
///  object of type <typeparamref name="TArg"/>.
/// </summary>
/// <typeparam name="T">The type of the objects in the span.</typeparam>
/// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
/// <param name="span">A span of objects of type <typeparamref name="T"/>.</param>
/// <param name="arg">A state object of type <typeparamref name="TArg"/>.</param>
/// <remarks>
///  On modern .NET, this delegate is declared with an 'allows ref struct' constraint on <typeparamref name="TArg"/>.
/// </remarks>
internal delegate void SpanAction<T, in TArg>(Span<T> span, TArg arg);
#endif
