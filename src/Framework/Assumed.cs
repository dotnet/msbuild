// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build;

/// <summary>
///  Provides internal assertion methods for validating assumptions in MSBuild code.
///  These should only be used to catch MSBuild bugs, not to validate user input.
///  Each method throws an <see cref="InternalErrorException"/> when the assertion fails.
///  Most methods have two overloads: one accepting an optional message string with caller
///  expression capture, and one accepting a conditional interpolated string handler that
///  avoids formatting allocations when the assertion passes. The <c>Unreachable</c>
///  methods additionally accept an <see cref="UnconditionalInterpolatedStringHandler"/>
///  since they always throw.
/// </summary>
internal static class Assumed
{
    /// <summary>
    ///  Asserts that <paramref name="value"/> is <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value being checked.</typeparam>
    /// <param name="value">The value expected to be <see langword="null"/>.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    /// <param name="valueExpression">The caller's source expression for <paramref name="value"/>, captured automatically.</param>
    public static void Null<T>(
        T? value,
        string? message = null,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
    {
        if (value is not null)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(valueExpression)} to be null.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is <see langword="null"/>, using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <typeparam name="T">The type of the value being checked.</typeparam>
    /// <param name="value">The value expected to be <see langword="null"/>.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the value is non-<see langword="null"/>.
    /// </param>
    public static void Null<T>(
        T? value,
        [InterpolatedStringHandlerArgument(nameof(value))] ref NullInterpolatedStringHandler<T> handler)
    {
        if (value is not null)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is not <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value being checked.</typeparam>
    /// <param name="value">The value expected to be non-null.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    /// <param name="valueExpression">The caller's source expression for <paramref name="value"/>, captured automatically.</param>
    public static void NotNull<T>(
        [NotNull] T? value,
        string? message = null,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
    {
        if (value is null)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(valueExpression)} to be non-null.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is not <see langword="null"/>, using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <typeparam name="T">The type of the value being checked.</typeparam>
    /// <param name="value">The value expected to be non-null.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the value is <see langword="null"/>.
    /// </param>
    public static void NotNull<T>(
        [NotNull] T? value,
        [InterpolatedStringHandlerArgument(nameof(value))] ref NotNullInterpolatedStringHandler<T> handler)
    {
        if (value is null)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is not <see langword="null"/> and not empty.
    /// </summary>
    /// <param name="value">The string expected to be non-null and non-empty.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    /// <param name="valueExpression">The caller's source expression for <paramref name="value"/>, captured automatically.</param>
    public static void NotNullOrEmpty(
        [NotNull] string? value,
        string? message = null,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
    {
        if (value is null or [])
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(valueExpression)} to be non-null and non-empty.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is not <see langword="null"/> and not empty, using
    ///  a conditional interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <param name="value">The string expected to be non-null and non-empty.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the value is <see langword="null"/> or empty.
    /// </param>
    public static void NotNullOrEmpty(
        [NotNull] string? value,
        [InterpolatedStringHandlerArgument(nameof(value))] ref NotNullOrEmptyInterpolatedStringHandler handler)
    {
        if (value is null or [])
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="collection"/> is not <see langword="null"/> and not empty.
    /// </summary>
    /// <typeparam name="T">The element type of the collection.</typeparam>
    /// <param name="collection">The collection expected to be non-null and non-empty.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    /// <param name="collectionExpression">The caller's source expression for <paramref name="collection"/>, captured automatically.</param>
    public static void NotNullOrEmpty<T>(
        [NotNull] IReadOnlyCollection<T>? collection,
        string? message = null,
        [CallerArgumentExpression(nameof(collection))] string? collectionExpression = null)
    {
        if (collection is null || collection.Count == 0)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(collectionExpression)} to be non-null and non-empty.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="collection"/> is not <see langword="null"/> and not empty, using
    ///  a conditional interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <typeparam name="T">The element type of the collection.</typeparam>
    /// <param name="collection">The collection expected to be non-null and non-empty.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the collection is <see langword="null"/> or empty.
    /// </param>
    public static void NotNullOrEmpty<T>(
        [NotNull] IReadOnlyCollection<T>? collection,
        [InterpolatedStringHandlerArgument(nameof(collection))] ref NotNullOrEmptyCollectionInterpolatedStringHandler<T> handler)
    {
        if (collection is null || collection.Count == 0)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="condition"/> is <see langword="true"/>.
    ///  This should be used to validate that internal assumptions hold, where failure
    ///  indicates a bug in MSBuild itself. Do not use for user input validation.
    /// </summary>
    /// <param name="condition">The condition expected to be <see langword="true"/>.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    /// <param name="conditionExpression">The caller's source expression for <paramref name="condition"/>, captured automatically.</param>
    public static void True(
        [DoesNotReturnIf(false)] bool condition,
        string? message = null,
        [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
    {
        if (!condition)
        {
            InternalError.Throw(message ?? $"{GetConditionString(conditionExpression)} expected to be true.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="condition"/> is <see langword="true"/>, using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <param name="condition">The condition expected to be <see langword="true"/>.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the condition is <see langword="false"/>.
    /// </param>
    public static void True(
        [DoesNotReturnIf(false)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref TrueInterpolatedStringHandler handler)
    {
        if (!condition)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition expected to be <see langword="false"/>.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    /// <param name="conditionExpression">The caller's source expression for <paramref name="condition"/>, captured automatically.</param>
    public static void False(
        [DoesNotReturnIf(true)] bool condition,
        string? message = null,
        [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
    {
        if (condition)
        {
            InternalError.Throw(message ?? $"{GetConditionString(conditionExpression)} expected to be false.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="condition"/> is <see langword="false"/>, using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <param name="condition">The condition expected to be <see langword="false"/>.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the condition is <see langword="true"/>.
    /// </param>
    public static void False(
        [DoesNotReturnIf(true)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref FalseInterpolatedStringHandler handler)
    {
        if (condition)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> equals <paramref name="other"/> using <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The expected value.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    /// <param name="valueExpression">The caller's source expression for <paramref name="value"/>, captured automatically.</param>
    public static void Equal<T>(
        T value,
        T other,
        string? message = null,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
    {
        if (!EqualityComparer<T>.Default.Equals(value, other))
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(valueExpression)} to be equal to '{GetStringOrNull(other)}'.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> equals <paramref name="other"/>, using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The expected value.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the values are not equal.
    /// </param>
    public static void Equal<T>(
        T value,
        T other,
        [InterpolatedStringHandlerArgument(nameof(value), nameof(other))] ref EqualInterpolatedStringHandler<T> handler)
    {
        if (!EqualityComparer<T>.Default.Equals(value, other))
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> equals <paramref name="other"/> using the specified
    ///  <see cref="StringComparison"/>.
    /// </summary>
    /// <param name="value">The actual string value.</param>
    /// <param name="other">The expected string value.</param>
    /// <param name="comparisonType">The <see cref="StringComparison"/> to use for the comparison.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    /// <param name="valueExpression">The caller's source expression for <paramref name="value"/>, captured automatically.</param>
    public static void Equal(
        string? value,
        string? other,
        StringComparison comparisonType,
        string? message = null,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
    {
        if (!string.Equals(value, other, comparisonType))
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(valueExpression)} to be equal to '{GetStringOrNull(other)}' ({comparisonType}).");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> equals <paramref name="other"/> using the specified
    ///  <see cref="StringComparison"/>, using a conditional interpolated string handler to avoid
    ///  formatting when the assertion passes.
    /// </summary>
    /// <param name="value">The actual string value.</param>
    /// <param name="other">The expected string value.</param>
    /// <param name="comparisonType">The <see cref="StringComparison"/> to use for the comparison.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the values are not equal.
    /// </param>
    public static void Equal(
        string? value,
        string? other,
        StringComparison comparisonType,
        [InterpolatedStringHandlerArgument(nameof(value), nameof(other), nameof(comparisonType))] ref StringEqualInterpolatedStringHandler handler)
    {
        if (!string.Equals(value, other, comparisonType))
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> does not equal <paramref name="other"/>
    ///  using <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The value that <paramref name="value"/> must not equal.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    /// <param name="valueExpression">The caller's source expression for <paramref name="value"/>, captured automatically.</param>
    public static void NotEqual<T>(
        T value,
        T other,
        string? message = null,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
    {
        if (EqualityComparer<T>.Default.Equals(value, other))
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(valueExpression)} to not be equal to '{GetStringOrNull(other)}'.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> does not equal <paramref name="other"/>, using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The value that <paramref name="value"/> must not equal.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the values are equal.
    /// </param>
    public static void NotEqual<T>(
        T value,
        T other,
        [InterpolatedStringHandlerArgument(nameof(value), nameof(other))] ref NotEqualInterpolatedStringHandler<T> handler)
    {
        if (EqualityComparer<T>.Default.Equals(value, other))
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> does not equal <paramref name="other"/> using the specified
    ///  <see cref="StringComparison"/>.
    /// </summary>
    /// <param name="value">The actual string value.</param>
    /// <param name="other">The value that <paramref name="value"/> must not equal.</param>
    /// <param name="comparisonType">The <see cref="StringComparison"/> to use for the comparison.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    /// <param name="valueExpression">The caller's source expression for <paramref name="value"/>, captured automatically.</param>
    public static void NotEqual(
        string? value,
        string? other,
        StringComparison comparisonType,
        string? message = null,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
    {
        if (string.Equals(value, other, comparisonType))
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(valueExpression)} to not be equal to '{GetStringOrNull(other)}' ({comparisonType}).");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> does not equal <paramref name="other"/> using the specified
    ///  <see cref="StringComparison"/>, using a conditional interpolated string handler to avoid
    ///  formatting when the assertion passes.
    /// </summary>
    /// <param name="value">The actual string value.</param>
    /// <param name="other">The value that <paramref name="value"/> must not equal.</param>
    /// <param name="comparisonType">The <see cref="StringComparison"/> to use for the comparison.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the values are equal.
    /// </param>
    public static void NotEqual(
        string? value,
        string? other,
        StringComparison comparisonType,
        [InterpolatedStringHandlerArgument(nameof(value), nameof(other), nameof(comparisonType))] ref StringNotEqualInterpolatedStringHandler handler)
    {
        if (string.Equals(value, other, comparisonType))
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is strictly greater than <paramref name="other"/>.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The value that <paramref name="value"/> must be greater than.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    public static void GreaterThan<T>(T value, T other, string? message = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) <= 0)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(value)} to be greater than '{GetStringOrNull(other)}'.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is strictly greater than <paramref name="other"/>, using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The value that <paramref name="value"/> must be greater than.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the assertion fails.
    /// </param>
    public static void GreaterThan<T>(
        T value,
        T other,
        [InterpolatedStringHandlerArgument(nameof(value), nameof(other))] ref GreaterThanInterpolatedStringHandler<T> handler)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) <= 0)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is greater than or equal to <paramref name="other"/>.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The value that <paramref name="value"/> must be greater than or equal to.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    public static void GreaterThanOrEqual<T>(T value, T other, string? message = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) < 0)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(value)} to be greater than or equal to '{GetStringOrNull(other)}'.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is greater than or equal to <paramref name="other"/>, using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The value that <paramref name="value"/> must be greater than or equal to.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the assertion fails.
    /// </param>
    public static void GreaterThanOrEqual<T>(
        T value,
        T other,
        [InterpolatedStringHandlerArgument(nameof(value), nameof(other))] ref GreaterThanOrEqualInterpolatedStringHandler<T> handler)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) < 0)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is strictly less than <paramref name="other"/>.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The value that <paramref name="value"/> must be less than.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    public static void LessThan<T>(T value, T other, string? message = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) >= 0)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(value)} to be less than '{GetStringOrNull(other)}'.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is strictly less than <paramref name="other"/>, using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The value that <paramref name="value"/> must be less than.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the assertion fails.
    /// </param>
    public static void LessThan<T>(
        T value,
        T other,
        [InterpolatedStringHandlerArgument(nameof(value), nameof(other))] ref LessThanInterpolatedStringHandler<T> handler)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) >= 0)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is less than or equal to <paramref name="other"/>.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The value that <paramref name="value"/> must be less than or equal to.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    public static void LessThanOrEqual<T>(T value, T other, string? message = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) > 0)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(value)} to be less than or equal to '{GetStringOrNull(other)}'.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is less than or equal to <paramref name="other"/>, using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="other">The value that <paramref name="value"/> must be less than or equal to.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the assertion fails.
    /// </param>
    public static void LessThanOrEqual<T>(
        T value,
        T other,
        [InterpolatedStringHandlerArgument(nameof(value), nameof(other))] ref LessThanOrEqualInterpolatedStringHandler<T> handler)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) > 0)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is zero.
    /// </summary>
    /// <param name="value">The integer value expected to be zero.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    public static void Zero(int value, string? message = null)
    {
        if (value != 0)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(value)} to be zero.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is zero, using a conditional interpolated string
    ///  handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <param name="value">The integer value expected to be zero.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the value is not zero.
    /// </param>
    public static void Zero(
        int value,
        [InterpolatedStringHandlerArgument(nameof(value))] ref ZeroInterpolatedStringHandler handler)
    {
        if (value != 0)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is negative (less than zero).
    /// </summary>
    /// <param name="value">The integer value expected to be negative.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    public static void Negative(int value, string? message = null)
    {
        if (value >= 0)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(value)} to be negative.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is negative (less than zero), using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <param name="value">The integer value expected to be negative.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the value is not negative.
    /// </param>
    public static void Negative(
        int value,
        [InterpolatedStringHandlerArgument(nameof(value))] ref NegativeInterpolatedStringHandler handler)
    {
        if (value >= 0)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is negative or zero (less than or equal to zero).
    /// </summary>
    /// <param name="value">The integer value expected to be negative or zero.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    public static void NegativeOrZero(int value, string? message = null)
    {
        if (value > 0)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(value)} to be negative or zero.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is negative or zero (less than or equal to zero), using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <param name="value">The integer value expected to be negative or zero.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the value is not negative or zero.
    /// </param>
    public static void NegativeOrZero(
        int value,
        [InterpolatedStringHandlerArgument(nameof(value))] ref NegativeOrZeroInterpolatedStringHandler handler)
    {
        if (value > 0)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is positive (greater than zero).
    /// </summary>
    /// <param name="value">The integer value expected to be positive.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    public static void Positive(int value, string? message = null)
    {
        if (value <= 0)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(value)} to be positive.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is positive (greater than zero), using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <param name="value">The integer value expected to be positive.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the value is not positive.
    /// </param>
    public static void Positive(
        int value,
        [InterpolatedStringHandlerArgument(nameof(value))] ref PositiveInterpolatedStringHandler handler)
    {
        if (value <= 0)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is positive or zero (greater than or equal to zero).
    /// </summary>
    /// <param name="value">The integer value expected to be positive or zero.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    public static void PositiveOrZero(int value, string? message = null)
    {
        if (value < 0)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(value)} to be positive or zero.");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is positive or zero (greater than or equal to zero), using a conditional
    ///  interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <param name="value">The integer value expected to be positive or zero.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the value is negative.
    /// </param>
    public static void PositiveOrZero(
        int value,
        [InterpolatedStringHandlerArgument(nameof(value))] ref PositiveOrZeroInterpolatedStringHandler handler)
    {
        if (value < 0)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is within the range [<paramref name="low"/>, <paramref name="high"/>],
    ///  i.e. greater than or equal to <paramref name="low"/> and less than or equal to <paramref name="high"/>.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="low">The inclusive lower bound.</param>
    /// <param name="high">The inclusive upper bound.</param>
    /// <param name="message">An optional error message. If not provided, a default message is generated.</param>
    public static void InRange<T>(T value, T low, T high, string? message = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(low) < 0 || value.CompareTo(high) > 0)
        {
            InternalError.Throw(message ?? $"Expected {GetValueString(value)} to be in range [{GetStringOrNull(low)}, {GetStringOrNull(high)}].");
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is within the range [<paramref name="low"/>, <paramref name="high"/>],
    ///  i.e. greater than or equal to <paramref name="low"/> and less than or equal to <paramref name="high"/>,
    ///  using a conditional interpolated string handler to avoid formatting when the assertion passes.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="value">The actual value.</param>
    /// <param name="low">The inclusive lower bound.</param>
    /// <param name="high">The inclusive upper bound.</param>
    /// <param name="handler">
    ///  The interpolated string handler that produces the error message only when the assertion fails.
    /// </param>
    public static void InRange<T>(
        T value,
        T low,
        T high,
        [InterpolatedStringHandlerArgument(nameof(value), nameof(low), nameof(high))] ref InRangeInterpolatedStringHandler<T> handler)
        where T : IComparable<T>
    {
        if (value.CompareTo(low) < 0 || value.CompareTo(high) > 0)
        {
            InternalError.Throw(handler.GetFormattedText());
        }
    }

    /// <summary>
    ///  Marks a code path as unreachable. Always throws an <see cref="InternalErrorException"/>.
    /// </summary>
    /// <param name="message">An optional error message. If not provided, defaults to "Unreachable code reached".</param>
    [DoesNotReturn]
    public static void Unreachable(string? message = null)
        => InternalError.Throw(message ?? "Unreachable code reached.");

    /// <summary>
    ///  Marks a code path as unreachable. Always throws an <see cref="InternalErrorException"/>
    ///  with a message constructed from an interpolated string handler that always formats its arguments.
    /// </summary>
    /// <param name="handler">The interpolated string handler that produces the error message.</param>
    [DoesNotReturn]
    public static void Unreachable(ref UnconditionalInterpolatedStringHandler handler)
        => InternalError.Throw(handler.GetFormattedText());

    /// <summary>
    ///  Marks a code path as unreachable and nominally returns a value of type <typeparamref name="T"/>.
    ///  Always throws an <see cref="InternalErrorException"/>. The return type allows use in expression contexts.
    /// </summary>
    /// <typeparam name="T">The nominal return type, used to satisfy expression contexts.</typeparam>
    /// <param name="message">An optional error message. If not provided, defaults to "Unreachable code reached".</param>
    /// <returns>
    ///  Never returns; always throws.
    /// </returns>
    [DoesNotReturn]
    public static T Unreachable<T>(string? message = null)
        => InternalError.Throw<T>(message ?? "Unreachable code reached.");

    /// <summary>
    ///  Marks a code path as unreachable and nominally returns a value of type <typeparamref name="T"/>.
    ///  Always throws an <see cref="InternalErrorException"/> with a message constructed from an
    ///  interpolated string handler that always formats its arguments. The return type allows use
    ///  in expression contexts.
    /// </summary>
    /// <typeparam name="T">The nominal return type, used to satisfy expression contexts.</typeparam>
    /// <param name="handler">The interpolated string handler that produces the error message.</param>
    /// <returns>
    ///  Never returns; always throws.
    /// </returns>
    [DoesNotReturn]
    public static T Unreachable<T>(ref UnconditionalInterpolatedStringHandler handler)
        => InternalError.Throw<T>(handler.GetFormattedText());

    /// <summary>
    ///  Formats a value for display in assertion error messages. Returns <c>'value'</c> if non-null,
    ///  or the literal string <c>"value"</c> as a fallback.
    /// </summary>
    private static string GetValueString<T>(T x)
        => x is not null ? $"'{x}'" : "value";

    /// <summary>
    ///  Formats a condition expression for display in assertion error messages. Returns <c>'expression'</c> if non-null,
    ///  or the literal string <c>"condition"</c> as a fallback.
    /// </summary>
    private static string GetConditionString<T>(T x)
        => x is not null ? $"'{x}'" : "condition";

    /// <summary>
    ///  Formats a value for display in assertion error messages. Returns <c>'value'</c> if non-null,
    ///  or the literal string <c>"null"</c>.
    /// </summary>
    private static string GetStringOrNull<T>(T x)
        => x is not null ? $"'{x}'" : "null";

    /// <summary>
    ///  Conditional interpolated string handler for <c>Null&lt;T&gt;(T?, ref NullInterpolatedStringHandler&lt;T&gt;})</c>.
    ///  Only formats the interpolated string when the value is not <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value being checked.</typeparam>
    [InterpolatedStringHandler]
    public ref struct NullInterpolatedStringHandler<T>
    {
        private StringBuilderHelper _builder;

        public NullInterpolatedStringHandler(int literalLength, int formattedCount, T? value, out bool isEnabled)
        {
            isEnabled = value is not null;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <c>NotNull&lt;T&gt;(T?, ref NotNullInterpolatedStringHandler&lt;T&gt;)</c>.
    ///  Only formats the interpolated string when the value is <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value being checked.</typeparam>
    [InterpolatedStringHandler]
    public ref struct NotNullInterpolatedStringHandler<T>
    {
        private StringBuilderHelper _builder;

        public NotNullInterpolatedStringHandler(int literalLength, int formattedCount, T? value, out bool isEnabled)
        {
            isEnabled = value is null;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for
    ///  <see cref="NotNullOrEmpty(string?, ref NotNullOrEmptyInterpolatedStringHandler)"/>.
    ///  Only formats the interpolated string when the value is <see langword="null"/> or empty.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct NotNullOrEmptyInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public NotNullOrEmptyInterpolatedStringHandler(int literalLength, int formattedCount, string? value, out bool isEnabled)
        {
            isEnabled = value is null or [];
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for the collection-based <c>NotNullOrEmpty</c> overload.
    ///  Only formats the interpolated string when the collection is <see langword="null"/> or empty.
    /// </summary>
    /// <typeparam name="T">The element type of the collection.</typeparam>
    [InterpolatedStringHandler]
    public ref struct NotNullOrEmptyCollectionInterpolatedStringHandler<T>
    {
        private StringBuilderHelper _builder;

        public NotNullOrEmptyCollectionInterpolatedStringHandler(int literalLength, int formattedCount, IReadOnlyCollection<T>? collection, out bool isEnabled)
        {
            isEnabled = collection is null || collection.Count == 0;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <see cref="True(bool, ref TrueInterpolatedStringHandler)"/>.
    ///  Only formats the interpolated string when the condition is <see langword="false"/>.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct TrueInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public TrueInterpolatedStringHandler(int literalLength, int formattedCount, bool condition, out bool isEnabled)
        {
            isEnabled = !condition;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <see cref="False(bool, ref FalseInterpolatedStringHandler)"/>.
    ///  Only formats the interpolated string when the condition is <see langword="true"/>.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct FalseInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public FalseInterpolatedStringHandler(int literalLength, int formattedCount, bool condition, out bool isEnabled)
        {
            isEnabled = condition;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <see cref="Equal{T}(T, T, ref EqualInterpolatedStringHandler{T})"/>.
    ///  Only formats the interpolated string when the values are not equal.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared.</typeparam>
    [InterpolatedStringHandler]
    public ref struct EqualInterpolatedStringHandler<T>
    {
        private StringBuilderHelper _builder;

        public EqualInterpolatedStringHandler(int literalLength, int formattedCount, T value, T other, out bool isEnabled)
        {
            isEnabled = !EqualityComparer<T>.Default.Equals(value, other);
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for
    ///  <see cref="Equal(string?, string?, StringComparison, ref StringEqualInterpolatedStringHandler)"/>.
    ///  Only formats the interpolated string when the strings are not equal using the specified comparison.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct StringEqualInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public StringEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? value, string? other, StringComparison comparisonType, out bool isEnabled)
        {
            isEnabled = !string.Equals(value, other, comparisonType);
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <see cref="NotEqual{T}(T, T, ref NotEqualInterpolatedStringHandler{T})"/>.
    ///  Only formats the interpolated string when the values are equal.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared.</typeparam>
    [InterpolatedStringHandler]
    public ref struct NotEqualInterpolatedStringHandler<T>
    {
        private StringBuilderHelper _builder;

        public NotEqualInterpolatedStringHandler(int literalLength, int formattedCount, T value, T other, out bool isEnabled)
        {
            isEnabled = EqualityComparer<T>.Default.Equals(value, other);
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for
    ///  <see cref="NotEqual(string?, string?, StringComparison, ref StringNotEqualInterpolatedStringHandler)"/>.
    ///  Only formats the interpolated string when the strings are equal using the specified comparison.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct StringNotEqualInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public StringNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? value, string? other, StringComparison comparisonType, out bool isEnabled)
        {
            isEnabled = string.Equals(value, other, comparisonType);
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <see cref="GreaterThan{T}(T, T, ref GreaterThanInterpolatedStringHandler{T})"/>.
    ///  Only formats the interpolated string when the value is not greater than the other.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    [InterpolatedStringHandler]
    public ref struct GreaterThanInterpolatedStringHandler<T>
        where T : IComparable<T>
    {
        private StringBuilderHelper _builder;

        public GreaterThanInterpolatedStringHandler(int literalLength, int formattedCount, T value, T other, out bool isEnabled)
        {
            isEnabled = value.CompareTo(other) <= 0;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for
    ///  <see cref="GreaterThanOrEqual{T}(T, T, ref GreaterThanOrEqualInterpolatedStringHandler{T})"/>.
    ///  Only formats the interpolated string when the value is less than the other.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    [InterpolatedStringHandler]
    public ref struct GreaterThanOrEqualInterpolatedStringHandler<T>
        where T : IComparable<T>
    {
        private StringBuilderHelper _builder;

        public GreaterThanOrEqualInterpolatedStringHandler(int literalLength, int formattedCount, T value, T other, out bool isEnabled)
        {
            isEnabled = value.CompareTo(other) < 0;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <see cref="LessThan{T}(T, T, ref LessThanInterpolatedStringHandler{T})"/>.
    ///  Only formats the interpolated string when the value is not less than the other.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    [InterpolatedStringHandler]
    public ref struct LessThanInterpolatedStringHandler<T>
        where T : IComparable<T>
    {
        private StringBuilderHelper _builder;

        public LessThanInterpolatedStringHandler(int literalLength, int formattedCount, T value, T other, out bool isEnabled)
        {
            isEnabled = value.CompareTo(other) >= 0;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for
    ///  <see cref="LessThanOrEqual{T}(T, T, ref LessThanOrEqualInterpolatedStringHandler{T})"/>.
    ///  Only formats the interpolated string when the value is greater than the other.
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    [InterpolatedStringHandler]
    public ref struct LessThanOrEqualInterpolatedStringHandler<T>
        where T : IComparable<T>
    {
        private StringBuilderHelper _builder;

        public LessThanOrEqualInterpolatedStringHandler(int literalLength, int formattedCount, T value, T other, out bool isEnabled)
        {
            isEnabled = value.CompareTo(other) > 0;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <see cref="Zero(int, ref ZeroInterpolatedStringHandler)"/>.
    ///  Only formats the interpolated string when the value is not zero.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct ZeroInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public ZeroInterpolatedStringHandler(int literalLength, int formattedCount, int value, out bool isEnabled)
        {
            isEnabled = value != 0;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <see cref="Negative(int, ref NegativeInterpolatedStringHandler)"/>.
    ///  Only formats the interpolated string when the value is not negative.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct NegativeInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public NegativeInterpolatedStringHandler(int literalLength, int formattedCount, int value, out bool isEnabled)
        {
            isEnabled = value >= 0;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <see cref="NegativeOrZero(int, ref NegativeOrZeroInterpolatedStringHandler)"/>.
    ///  Only formats the interpolated string when the value is not negative or zero.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct NegativeOrZeroInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public NegativeOrZeroInterpolatedStringHandler(int literalLength, int formattedCount, int value, out bool isEnabled)
        {
            isEnabled = value > 0;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <see cref="Positive(int, ref PositiveInterpolatedStringHandler)"/>.
    ///  Only formats the interpolated string when the value is not positive.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct PositiveInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public PositiveInterpolatedStringHandler(int literalLength, int formattedCount, int value, out bool isEnabled)
        {
            isEnabled = value <= 0;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for
    ///  <see cref="PositiveOrZero(int, ref PositiveOrZeroInterpolatedStringHandler)"/>.
    ///  Only formats the interpolated string when the value is negative.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct PositiveOrZeroInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public PositiveOrZeroInterpolatedStringHandler(int literalLength, int formattedCount, int value, out bool isEnabled)
        {
            isEnabled = value < 0;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Conditional interpolated string handler for <see cref="InRange{T}(T, T, T, ref InRangeInterpolatedStringHandler{T})"/>.
    ///  Only formats the interpolated string when the value is outside the range [low, high].
    /// </summary>
    /// <typeparam name="T">The type of the values being compared. Must implement <see cref="IComparable{T}"/>.</typeparam>
    [InterpolatedStringHandler]
    public ref struct InRangeInterpolatedStringHandler<T>
        where T : IComparable<T>
    {
        private StringBuilderHelper _builder;

        public InRangeInterpolatedStringHandler(int literalLength, int formattedCount, T value, T low, T high, out bool isEnabled)
        {
            isEnabled = value.CompareTo(low) < 0 || value.CompareTo(high) > 0;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }
}
