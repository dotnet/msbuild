// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System;
#endif
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

    extension(string)
    {
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
