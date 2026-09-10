// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Provides helpers for formatting composite format strings, optionally with a specific culture.
///  When no arguments are supplied the format string is returned unchanged, and in debug builds
///  each argument's type is validated to catch values that lack a meaningful <see cref="object.ToString()"/>
///  override.
/// </summary>
internal static class MessageFormatter
{
    /// <summary>
    ///  Formats <paramref name="format"/> as a composite format string, using the current culture.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    public static string Format(string format, object? arg0)
        => Format(culture: null, format, arg0);

    /// <summary>
    ///  Formats <paramref name="format"/> as a composite format string, using the current culture.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first object to substitute into the format string.</param>
    /// <param name="arg1">The second object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    public static string Format(string format, object? arg0, object? arg1)
        => Format(culture: null, format, arg0, arg1);

    /// <summary>
    ///  Formats <paramref name="format"/> as a composite format string, using the current culture.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first object to substitute into the format string.</param>
    /// <param name="arg1">The second object to substitute into the format string.</param>
    /// <param name="arg2">The third object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    public static string Format(string format, object? arg0, object? arg1, object? arg2)
        => Format(culture: null, format, arg0, arg1, arg2);

    /// <summary>
    ///  Formats <paramref name="format"/> as a composite format string, using the current culture. When
    ///  <paramref name="args"/> is <see langword="null"/> or empty, <paramref name="format"/> is returned unchanged.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The objects to substitute into the format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    /// <remarks>
    ///  PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is
    ///  allocated for the array of arguments -- do not call this method repeatedly in performance-critical scenarios.
    /// </remarks>
    public static string Format(string format, params object?[] args)
        => Format(culture: null, format, args);

#if NET
    /// <summary>
    ///  Formats <paramref name="format"/> as a composite format string, using the current culture. When
    ///  <paramref name="args"/> is empty, <paramref name="format"/> is returned unchanged.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The objects to substitute into the format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    public static string Format(string format, params ReadOnlySpan<object?> args)
        => Format(culture: null, format, args);
#endif

    /// <summary>
    ///  Formats <paramref name="format"/> as a composite format string, using the specified culture.
    /// </summary>
    /// <param name="culture">The culture used to format the substituted values, or <see langword="null"/> to use the current culture.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    public static string Format(CultureInfo? culture, string format, object? arg0)
    {
        ValidateArg(arg0);

        return string.Format(culture, format, arg0);
    }

    /// <summary>
    ///  Formats <paramref name="format"/> as a composite format string, using the specified culture.
    /// </summary>
    /// <param name="culture">The culture used to format the substituted values, or <see langword="null"/> to use the current culture.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first object to substitute into the format string.</param>
    /// <param name="arg1">The second object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    public static string Format(CultureInfo? culture, string format, object? arg0, object? arg1)
    {
        ValidateArg(arg0);
        ValidateArg(arg1);

        return string.Format(culture, format, arg0, arg1);
    }

    /// <summary>
    ///  Formats <paramref name="format"/> as a composite format string, using the specified culture.
    /// </summary>
    /// <param name="culture">The culture used to format the substituted values, or <see langword="null"/> to use the current culture.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first object to substitute into the format string.</param>
    /// <param name="arg1">The second object to substitute into the format string.</param>
    /// <param name="arg2">The third object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    public static string Format(CultureInfo? culture, string format, object? arg0, object? arg1, object? arg2)
    {
        ValidateArg(arg0);
        ValidateArg(arg1);
        ValidateArg(arg2);

        return string.Format(culture, format, arg0, arg1, arg2);
    }

    /// <summary>
    ///  Formats <paramref name="format"/> as a composite format string, using the specified culture. When
    ///  <paramref name="args"/> is <see langword="null"/> or empty, <paramref name="format"/> is returned unchanged.
    /// </summary>
    /// <param name="culture">The culture used to format the substituted values, or <see langword="null"/> to use the current culture.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The objects to substitute into the format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    /// <remarks>
    ///  PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is
    ///  allocated for the array of arguments -- do not call this method repeatedly in performance-critical scenarios.
    /// </remarks>
    public static string Format(CultureInfo? culture, string format, params object?[] args)
    {
        if (args is null or [])
        {
            return format;
        }

        ValidateArgs(args);

        return string.Format(culture, format, args);
    }

#if NET
    /// <summary>
    ///  Formats <paramref name="format"/> as a composite format string, using the specified culture. When
    ///  <paramref name="args"/> is empty, <paramref name="format"/> is returned unchanged.
    /// </summary>
    /// <param name="culture">The culture used to format the substituted values, or <see langword="null"/> to use the current culture.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The objects to substitute into the format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    public static string Format(CultureInfo? culture, string format, params ReadOnlySpan<object?> args)
    {
        if (args is [])
        {
            return format;
        }

        ValidateArgs(args);

        return string.Format(culture, format, args);
    }
#endif

    [Conditional("DEBUG")]
    private static void ValidateArgs(object?[] args)
    {
        foreach (object? arg in args)
        {
            ValidateArg(arg);
        }
    }

#if NET
    [Conditional("DEBUG")]
    private static void ValidateArgs(ReadOnlySpan<object?> args)
    {
        foreach (object? arg in args)
        {
            ValidateArg(arg);
        }
    }
#endif

    [Conditional("DEBUG")]
    private static void ValidateArg(object? arg)
    {
        if (arg is null)
        {
            return;
        }

        Type argType = arg.GetType();
        if (argType == typeof(string))
        {
            return;
        }

        // If you accidentally pass some random type in that can't be converted to a string,
        // String.Format calls ToString() which returns the full name of the type!
        Assumed.NotEqual(argType.ToString(), arg.ToString(), StringComparison.Ordinal, $"Invalid resource arg type, was {argType.FullName}");
    }
}
