// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Microsoft.Build;

internal static partial class ArgumentExceptionExtensions
{
    extension(ArgumentNullException)
    {
        /// <summary>
        ///  Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is <see langword="null"/>.
        /// </summary>
        /// <param name="argument">
        ///  The reference type argument to validate as non-null.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="argument"/> corresponds.
        ///  If you omit this parameter, the name of <paramref name="argument"/> is used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///  <paramref name="argument"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        ///  The <paramref name="paramName"/> parameter is included to support the <see cref="CallerArgumentExpressionAttribute"/>
        ///  attribute. It's recommended that you don't pass a value for this parameter and let the name of
        ///  <paramref name="argument"/> be used instead.
        /// </remarks>
        public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null)
            {
                ThrowNull(paramName);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is <see langword="null"/>.
        /// </summary>
        /// <param name="argument">
        ///  The pointer argument to validate as non-null.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="argument"/> corresponds.
        ///  If you omit this parameter, the name of <paramref name="argument"/> is used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///  <paramref name="argument"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        ///  The <paramref name="paramName"/> parameter is included to support the <see cref="CallerArgumentExpressionAttribute"/>
        ///  attribute. It's recommended that you don't pass a value for this parameter and let the name of
        ///  <paramref name="argument"/> be used instead.
        /// </remarks>
        public static unsafe void ThrowIfNull(void* argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null)
            {
                ThrowNull(paramName);
            }
        }
    }

    extension(ArgumentException)
    {
        /// <summary>
        ///  Throws an exception if <paramref name="argument"/> is <see langword="null"/> or empty.
        /// </summary>
        /// <param name="argument">
        ///  The string argument to validate as non-null and non-empty.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="argument"/> corresponds.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///  <paramref name="argument"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///  <paramref name="argument"/> is empty.
        /// </exception>
        public static void ThrowIfNullOrEmpty([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (string.IsNullOrEmpty(argument))
            {
                ThrowNullOrEmpty(argument, paramName);
            }

            Contract.ThrowIfNull(argument);
        }

        /// <summary>
        ///  Throws an exception if <paramref name="argument"/> is <see langword="null"/>, empty,
        ///  or consists only of white-space characters.
        /// </summary>
        /// <param name="argument">
        ///  The string argument to validate.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="argument"/> corresponds.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///  <paramref name="argument"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///  <paramref name="argument"/> is empty or consists only of white-space characters.
        /// </exception>
        public static void ThrowIfNullOrWhiteSpace([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                ThrowNullOrWhiteSpace(argument, paramName);
            }

            Contract.ThrowIfNull(argument);
        }
    }

    extension(ArgumentOutOfRangeException)
    {
        /// <summary>
        ///  Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is zero.
        /// </summary>
        /// <param name="value">
        ///  The argument to validate as non-zero.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="value"/> corresponds.
        /// </param>
        public static void ThrowIfZero(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value == 0)
            {
                ThrowZero(value, paramName);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is negative.
        /// </summary>
        /// <param name="value">
        ///  The argument to validate as non-negative.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="value"/> corresponds.
        /// </param>
        public static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value < 0)
            {
                ThrowNegative(value, paramName);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is negative or zero.
        /// </summary>
        /// <param name="value">
        ///  The argument to validate as non-zero or non-negative.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="value"/> corresponds.
        /// </param>
        public static void ThrowIfNegativeOrZero(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value <= 0)
            {
                ThrowNegativeOrZero(value, paramName);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is equal to <paramref name="other"/>.
        /// </summary>
        /// <param name="value">
        ///  The argument to validate as not equal to <paramref name="other"/>.
        /// </param>
        /// <param name="other">
        ///  The value to compare with <paramref name="value"/>.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="value"/> corresponds.
        /// </param>
        public static void ThrowIfEqual<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (EqualityComparer<T>.Default.Equals(value, other))
            {
                ThrowEqual(value, other, paramName);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is not equal to <paramref name="other"/>.
        /// </summary>
        /// <param name="value">
        ///  The argument to validate as equal to <paramref name="other"/>.
        /// </param>
        /// <param name="other">
        ///  The value to compare with <paramref name="value"/>.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="value"/> corresponds.
        /// </param>
        public static void ThrowIfNotEqual<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(value, other))
            {
                ThrowNotEqual(value, other, paramName);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is greater than <paramref name="other"/>.
        /// </summary>
        /// <param name="value">
        ///  The argument to validate as less or equal than <paramref name="other"/>.
        /// </param>
        /// <param name="other">
        ///  The value to compare with <paramref name="value"/>.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="value"/> corresponds.
        /// </param>
        public static void ThrowIfGreaterThan<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) > 0)
            {
                ThrowGreater(value, other, paramName);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is greater than or equal <paramref name="other"/>.
        /// </summary>
        /// <param name="value">
        ///  The argument to validate as less than <paramref name="other"/>.
        /// </param>
        /// <param name="other">
        ///  The value to compare with <paramref name="value"/>.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="value"/> corresponds.
        /// </param>
        public static void ThrowIfGreaterThanOrEqual<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) >= 0)
            {
                ThrowGreaterEqual(value, other, paramName);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is less than <paramref name="other"/>.
        /// </summary>
        /// <param name="value">
        ///  The argument to validate as greater than or equal than <paramref name="other"/>.
        /// </param>
        /// <param name="other">
        ///  The value to compare with <paramref name="value"/>.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="value"/> corresponds.
        /// </param>
        public static void ThrowIfLessThan<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) < 0)
            {
                ThrowLess(value, other, paramName);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is less than or equal <paramref name="other"/>.
        /// </summary>
        /// <param name="value">
        ///  The argument to validate as greater than than <paramref name="other"/>.
        /// </param>
        /// <param name="other">
        ///  The value to compare with <paramref name="value"/>.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="value"/> corresponds.
        /// </param>
        public static void ThrowIfLessThanOrEqual<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) <= 0)
            {
                ThrowLessEqual(value, other, paramName);
            }
        }
    }

    extension(ObjectDisposedException)
    {
        /// <summary>
        ///  Throws an <see cref="ObjectDisposedException"/> if the specified <paramref name="condition"/> is <see langword="true"/>.
        /// </summary>
        /// <param name="condition">
        ///  The condition to evaluate.
        /// </param>
        /// <param name="instance">
        ///  The object whose type's full name should be included in any resulting <see cref="ObjectDisposedException"/>.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        ///  The <paramref name="condition"/> is <see langword="true"/>.
        /// </exception>
        public static void ThrowIf([DoesNotReturnIf(true)] bool condition, object instance)
        {
            if (condition)
            {
                ThrowObjectDisposed(instance);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ObjectDisposedException"/> if the specified <paramref name="condition"/> is <see langword="true"/>.
        /// </summary>
        /// <param name="condition">
        ///  The condition to evaluate.
        /// </param>
        /// <param name="type">
        ///  The type whose full name should be included in any resulting <see cref="ObjectDisposedException"/>.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        ///  The <paramref name="condition"/> is <see langword="true"/>.
        /// </exception>
        public static void ThrowIf([DoesNotReturnIf(true)] bool condition, Type type)
        {
            if (condition)
            {
                ThrowObjectDisposed(type);
            }
        }
    }

#pragma warning disable IDE0051 // Private member is unused.

    [DoesNotReturn]
    private static void ThrowNull(string? paramName)
        => throw new ArgumentNullException(paramName);

    [DoesNotReturn]
    private static void ThrowNullOrEmpty(string? argument, string? paramName)
    {
        ArgumentNullException.ThrowIfNull(argument, paramName);
        throw new ArgumentException(SR.Argument_EmptyString, paramName);
    }

    [DoesNotReturn]
    private static void ThrowNullOrWhiteSpace(string? argument, string? paramName)
    {
        ArgumentNullException.ThrowIfNull(argument, paramName);
        throw new ArgumentException(SR.Argument_EmptyOrWhiteSpaceString, paramName);
    }

    [DoesNotReturn]
    private static void ThrowZero<T>(T value, string? paramName)
        => throw new ArgumentOutOfRangeException(paramName, value, SR.FormatArgumentOutOfRange_Generic_MustBeNonZero(paramName, value));

    [DoesNotReturn]
    private static void ThrowNegative<T>(T value, string? paramName)
        => throw new ArgumentOutOfRangeException(paramName, value, SR.FormatArgumentOutOfRange_Generic_MustBeNonNegative(paramName, value));

    [DoesNotReturn]
    private static void ThrowNegativeOrZero<T>(T value, string? paramName)
        => throw new ArgumentOutOfRangeException(paramName, value, SR.FormatArgumentOutOfRange_Generic_MustBeNonNegativeNonZero(paramName, value));

    [DoesNotReturn]
    private static void ThrowGreater<T>(T value, T other, string? paramName)
        => throw new ArgumentOutOfRangeException(paramName, value, SR.FormatArgumentOutOfRange_Generic_MustBeLessOrEqual(paramName, value, other));

    [DoesNotReturn]
    private static void ThrowGreaterEqual<T>(T value, T other, string? paramName)
        => throw new ArgumentOutOfRangeException(paramName, value, SR.FormatArgumentOutOfRange_Generic_MustBeLess(paramName, value, other));

    [DoesNotReturn]
    private static void ThrowLess<T>(T value, T other, string? paramName)
        => throw new ArgumentOutOfRangeException(paramName, value, SR.FormatArgumentOutOfRange_Generic_MustBeGreaterOrEqual(paramName, value, other));

    [DoesNotReturn]
    private static void ThrowLessEqual<T>(T value, T other, string? paramName)
        => throw new ArgumentOutOfRangeException(paramName, value, SR.FormatArgumentOutOfRange_Generic_MustBeGreater(paramName, value, other));

    [DoesNotReturn]
    private static void ThrowEqual<T>(T value, T other, string? paramName)
        => throw new ArgumentOutOfRangeException(paramName, value, SR.FormatArgumentOutOfRange_Generic_MustBeNotEqual(paramName, (object?)value ?? "null", (object?)other ?? "null"));

    [DoesNotReturn]
    private static void ThrowNotEqual<T>(T value, T other, string? paramName)
        => throw new ArgumentOutOfRangeException(paramName, value, SR.FormatArgumentOutOfRange_Generic_MustBeEqual(paramName, (object?)value ?? "null", (object?)other ?? "null"));

    [DoesNotReturn]
    private static void ThrowObjectDisposed(object? instance)
        => throw new ObjectDisposedException(instance?.GetType().FullName);

    [DoesNotReturn]
    private static void ThrowObjectDisposed(Type? type)
        => throw new ObjectDisposedException(type?.FullName);
}
#endif
