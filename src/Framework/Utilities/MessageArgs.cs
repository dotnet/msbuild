// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Provides indexed access and formatting for deferred message args.
/// </summary>
internal interface IMessageArgs
{
    /// <summary>
    ///  Gets the number of args.
    /// </summary>
    /// <value>
    ///  The number of args.
    /// </value>
    int Count { get; }

    /// <summary>
    ///  Gets an arg by index.
    /// </summary>
    /// <param name="index">The zero-based arg index.</param>
    /// <returns>
    ///  The arg.
    /// </returns>
    object? this[int index] { get; }

    /// <summary>
    ///  Formats the args into a composite format string.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format);
}

/// <summary>
///  Represents an empty deferred arg set and creates strongly typed deferred message args.
/// </summary>
internal readonly struct MessageArgs : IMessageArgs
{
    public int Count => 0;

    public object? this[int index]
    {
        get
        {
            Assumed.InRange(index, 0, Count - 1);
            return Assumed.Unreachable<object?>();
        }
    }

    public string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
        => format;

    /// <summary>
    ///  Creates deferred message args containing one value.
    /// </summary>
    /// <typeparam name="T1">The type of the first arg.</typeparam>
    /// <param name="arg1">The first arg.</param>
    /// <returns>
    ///  The deferred message args.
    /// </returns>
    public static MessageArgs<T1> Create<T1>(T1 arg1)
        => new(arg1);

    /// <summary>
    ///  Creates deferred message args containing two values.
    /// </summary>
    /// <typeparam name="T1">The type of the first arg.</typeparam>
    /// <typeparam name="T2">The type of the second arg.</typeparam>
    /// <param name="arg1">The first arg.</param>
    /// <param name="arg2">The second arg.</param>
    /// <returns>
    ///  The deferred message args.
    /// </returns>
    public static MessageArgs<T1, T2> Create<T1, T2>(T1 arg1, T2 arg2)
        => new(arg1, arg2);

    /// <summary>
    ///  Creates deferred message args containing three values.
    /// </summary>
    /// <typeparam name="T1">The type of the first arg.</typeparam>
    /// <typeparam name="T2">The type of the second arg.</typeparam>
    /// <typeparam name="T3">The type of the third arg.</typeparam>
    /// <param name="arg1">The first arg.</param>
    /// <param name="arg2">The second arg.</param>
    /// <param name="arg3">The third arg.</param>
    /// <returns>
    ///  The deferred message args.
    /// </returns>
    public static MessageArgs<T1, T2, T3> Create<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3)
        => new(arg1, arg2, arg3);

    /// <summary>
    ///  Creates deferred message args containing four values.
    /// </summary>
    /// <typeparam name="T1">The type of the first arg.</typeparam>
    /// <typeparam name="T2">The type of the second arg.</typeparam>
    /// <typeparam name="T3">The type of the third arg.</typeparam>
    /// <typeparam name="T4">The type of the fourth arg.</typeparam>
    /// <param name="arg1">The first arg.</param>
    /// <param name="arg2">The second arg.</param>
    /// <param name="arg3">The third arg.</param>
    /// <param name="arg4">The fourth arg.</param>
    /// <returns>
    ///  The deferred message args.
    /// </returns>
    public static MessageArgs<T1, T2, T3, T4> Create<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        => new(arg1, arg2, arg3, arg4);

    /// <summary>
    ///  Creates deferred message args containing five values.
    /// </summary>
    /// <typeparam name="T1">The type of the first arg.</typeparam>
    /// <typeparam name="T2">The type of the second arg.</typeparam>
    /// <typeparam name="T3">The type of the third arg.</typeparam>
    /// <typeparam name="T4">The type of the fourth arg.</typeparam>
    /// <typeparam name="T5">The type of the fifth arg.</typeparam>
    /// <param name="arg1">The first arg.</param>
    /// <param name="arg2">The second arg.</param>
    /// <param name="arg3">The third arg.</param>
    /// <param name="arg4">The fourth arg.</param>
    /// <param name="arg5">The fifth arg.</param>
    /// <returns>
    ///  The deferred message args.
    /// </returns>
    public static MessageArgs<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        => new(arg1, arg2, arg3, arg4, arg5);

    /// <summary>
    ///  Creates deferred message args containing six values.
    /// </summary>
    /// <typeparam name="T1">The type of the first arg.</typeparam>
    /// <typeparam name="T2">The type of the second arg.</typeparam>
    /// <typeparam name="T3">The type of the third arg.</typeparam>
    /// <typeparam name="T4">The type of the fourth arg.</typeparam>
    /// <typeparam name="T5">The type of the fifth arg.</typeparam>
    /// <typeparam name="T6">The type of the sixth arg.</typeparam>
    /// <param name="arg1">The first arg.</param>
    /// <param name="arg2">The second arg.</param>
    /// <param name="arg3">The third arg.</param>
    /// <param name="arg4">The fourth arg.</param>
    /// <param name="arg5">The fifth arg.</param>
    /// <param name="arg6">The sixth arg.</param>
    /// <returns>
    ///  The deferred message args.
    /// </returns>
    public static MessageArgs<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        => new(arg1, arg2, arg3, arg4, arg5, arg6);

    /// <summary>
    ///  Creates deferred message args containing seven values.
    /// </summary>
    /// <typeparam name="T1">The type of the first arg.</typeparam>
    /// <typeparam name="T2">The type of the second arg.</typeparam>
    /// <typeparam name="T3">The type of the third arg.</typeparam>
    /// <typeparam name="T4">The type of the fourth arg.</typeparam>
    /// <typeparam name="T5">The type of the fifth arg.</typeparam>
    /// <typeparam name="T6">The type of the sixth arg.</typeparam>
    /// <typeparam name="T7">The type of the seventh arg.</typeparam>
    /// <param name="arg1">The first arg.</param>
    /// <param name="arg2">The second arg.</param>
    /// <param name="arg3">The third arg.</param>
    /// <param name="arg4">The fourth arg.</param>
    /// <param name="arg5">The fifth arg.</param>
    /// <param name="arg6">The sixth arg.</param>
    /// <param name="arg7">The seventh arg.</param>
    /// <returns>
    ///  The deferred message args.
    /// </returns>
    public static MessageArgs<T1, T2, T3, T4, T5, T6, T7> Create<T1, T2, T3, T4, T5, T6, T7>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        => new(arg1, arg2, arg3, arg4, arg5, arg6, arg7);

    /// <summary>
    ///  Creates deferred message args containing eight values.
    /// </summary>
    /// <typeparam name="T1">The type of the first arg.</typeparam>
    /// <typeparam name="T2">The type of the second arg.</typeparam>
    /// <typeparam name="T3">The type of the third arg.</typeparam>
    /// <typeparam name="T4">The type of the fourth arg.</typeparam>
    /// <typeparam name="T5">The type of the fifth arg.</typeparam>
    /// <typeparam name="T6">The type of the sixth arg.</typeparam>
    /// <typeparam name="T7">The type of the seventh arg.</typeparam>
    /// <typeparam name="T8">The type of the eighth arg.</typeparam>
    /// <param name="arg1">The first arg.</param>
    /// <param name="arg2">The second arg.</param>
    /// <param name="arg3">The third arg.</param>
    /// <param name="arg4">The fourth arg.</param>
    /// <param name="arg5">The fifth arg.</param>
    /// <param name="arg6">The sixth arg.</param>
    /// <param name="arg7">The seventh arg.</param>
    /// <param name="arg8">The eighth arg.</param>
    /// <returns>
    ///  The deferred message args.
    /// </returns>
    public static MessageArgs<T1, T2, T3, T4, T5, T6, T7, T8> Create<T1, T2, T3, T4, T5, T6, T7, T8>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        => new(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
}

/// <summary>
///  Stores one deferred message arg.
/// </summary>
/// <typeparam name="T1">The type of the first arg.</typeparam>
/// <param name="arg1">The first arg.</param>
internal readonly struct MessageArgs<T1>(T1 arg1) : IMessageArgs
{
    public int Count => 1;

    public object? this[int index]
    {
        get
        {
            Assumed.InRange(index, 0, Count - 1);
            return arg1;
        }
    }

    public string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
        => MessageFormatter.Format(format, arg1);
}

/// <summary>
///  Stores two deferred message args.
/// </summary>
/// <typeparam name="T1">The type of the first arg.</typeparam>
/// <typeparam name="T2">The type of the second arg.</typeparam>
/// <param name="arg1">The first arg.</param>
/// <param name="arg2">The second arg.</param>
internal readonly struct MessageArgs<T1, T2>(T1 arg1, T2 arg2) : IMessageArgs
{
    public int Count => 2;

    public object? this[int index]
    {
        get
        {
            Assumed.InRange(index, 0, Count - 1);
            return index switch
            {
                0 => arg1,
                1 => arg2,
                _ => Assumed.Unreachable<object?>(),
            };
        }
    }

    public string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
        => MessageFormatter.Format(format, arg1, arg2);
}

/// <summary>
///  Stores three deferred message args.
/// </summary>
/// <typeparam name="T1">The type of the first arg.</typeparam>
/// <typeparam name="T2">The type of the second arg.</typeparam>
/// <typeparam name="T3">The type of the third arg.</typeparam>
/// <param name="arg1">The first arg.</param>
/// <param name="arg2">The second arg.</param>
/// <param name="arg3">The third arg.</param>
internal readonly struct MessageArgs<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3) : IMessageArgs
{
    public int Count => 3;

    public object? this[int index]
    {
        get
        {
            Assumed.InRange(index, 0, Count - 1);
            return index switch
            {
                0 => arg1,
                1 => arg2,
                2 => arg3,
                _ => Assumed.Unreachable<object?>(),
            };
        }
    }

    public string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
        => MessageFormatter.Format(format, arg1, arg2, arg3);
}

/// <summary>
///  Stores four deferred message args.
/// </summary>
/// <typeparam name="T1">The type of the first arg.</typeparam>
/// <typeparam name="T2">The type of the second arg.</typeparam>
/// <typeparam name="T3">The type of the third arg.</typeparam>
/// <typeparam name="T4">The type of the fourth arg.</typeparam>
/// <param name="arg1">The first arg.</param>
/// <param name="arg2">The second arg.</param>
/// <param name="arg3">The third arg.</param>
/// <param name="arg4">The fourth arg.</param>
internal readonly struct MessageArgs<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4) : IMessageArgs
{
    public int Count => 4;

    public object? this[int index]
    {
        get
        {
            Assumed.InRange(index, 0, Count - 1);
            return index switch
            {
                0 => arg1,
                1 => arg2,
                2 => arg3,
                3 => arg4,
                _ => Assumed.Unreachable<object?>(),
            };
        }
    }

    public string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
        => MessageFormatter.Format(format, [arg1, arg2, arg3, arg4]);
}

/// <summary>
///  Stores five deferred message args.
/// </summary>
/// <typeparam name="T1">The type of the first arg.</typeparam>
/// <typeparam name="T2">The type of the second arg.</typeparam>
/// <typeparam name="T3">The type of the third arg.</typeparam>
/// <typeparam name="T4">The type of the fourth arg.</typeparam>
/// <typeparam name="T5">The type of the fifth arg.</typeparam>
/// <param name="arg1">The first arg.</param>
/// <param name="arg2">The second arg.</param>
/// <param name="arg3">The third arg.</param>
/// <param name="arg4">The fourth arg.</param>
/// <param name="arg5">The fifth arg.</param>
internal readonly struct MessageArgs<T1, T2, T3, T4, T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) : IMessageArgs
{
    public int Count => 5;

    public object? this[int index]
    {
        get
        {
            Assumed.InRange(index, 0, Count - 1);
            return index switch
            {
                0 => arg1,
                1 => arg2,
                2 => arg3,
                3 => arg4,
                4 => arg5,
                _ => Assumed.Unreachable<object?>(),
            };
        }
    }

    public string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
        => MessageFormatter.Format(format, [arg1, arg2, arg3, arg4, arg5]);
}

/// <summary>
///  Stores six deferred message args.
/// </summary>
/// <typeparam name="T1">The type of the first arg.</typeparam>
/// <typeparam name="T2">The type of the second arg.</typeparam>
/// <typeparam name="T3">The type of the third arg.</typeparam>
/// <typeparam name="T4">The type of the fourth arg.</typeparam>
/// <typeparam name="T5">The type of the fifth arg.</typeparam>
/// <typeparam name="T6">The type of the sixth arg.</typeparam>
/// <param name="arg1">The first arg.</param>
/// <param name="arg2">The second arg.</param>
/// <param name="arg3">The third arg.</param>
/// <param name="arg4">The fourth arg.</param>
/// <param name="arg5">The fifth arg.</param>
/// <param name="arg6">The sixth arg.</param>
internal readonly struct MessageArgs<T1, T2, T3, T4, T5, T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6) : IMessageArgs
{
    public int Count => 6;

    public object? this[int index]
    {
        get
        {
            Assumed.InRange(index, 0, Count - 1);
            return index switch
            {
                0 => arg1,
                1 => arg2,
                2 => arg3,
                3 => arg4,
                4 => arg5,
                5 => arg6,
                _ => Assumed.Unreachable<object?>(),
            };
        }
    }

    public string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
        => MessageFormatter.Format(format, [arg1, arg2, arg3, arg4, arg5, arg6]);
}

/// <summary>
///  Stores seven deferred message args.
/// </summary>
/// <typeparam name="T1">The type of the first arg.</typeparam>
/// <typeparam name="T2">The type of the second arg.</typeparam>
/// <typeparam name="T3">The type of the third arg.</typeparam>
/// <typeparam name="T4">The type of the fourth arg.</typeparam>
/// <typeparam name="T5">The type of the fifth arg.</typeparam>
/// <typeparam name="T6">The type of the sixth arg.</typeparam>
/// <typeparam name="T7">The type of the seventh arg.</typeparam>
/// <param name="arg1">The first arg.</param>
/// <param name="arg2">The second arg.</param>
/// <param name="arg3">The third arg.</param>
/// <param name="arg4">The fourth arg.</param>
/// <param name="arg5">The fifth arg.</param>
/// <param name="arg6">The sixth arg.</param>
/// <param name="arg7">The seventh arg.</param>
internal readonly struct MessageArgs<T1, T2, T3, T4, T5, T6, T7>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7) : IMessageArgs
{
    public int Count => 7;

    public object? this[int index]
    {
        get
        {
            Assumed.InRange(index, 0, Count - 1);
            return index switch
            {
                0 => arg1,
                1 => arg2,
                2 => arg3,
                3 => arg4,
                4 => arg5,
                5 => arg6,
                6 => arg7,
                _ => Assumed.Unreachable<object?>(),
            };
        }
    }

    public string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
        => MessageFormatter.Format(format, [arg1, arg2, arg3, arg4, arg5, arg6, arg7]);
}

/// <summary>
///  Stores eight deferred message args.
/// </summary>
/// <typeparam name="T1">The type of the first arg.</typeparam>
/// <typeparam name="T2">The type of the second arg.</typeparam>
/// <typeparam name="T3">The type of the third arg.</typeparam>
/// <typeparam name="T4">The type of the fourth arg.</typeparam>
/// <typeparam name="T5">The type of the fifth arg.</typeparam>
/// <typeparam name="T6">The type of the sixth arg.</typeparam>
/// <typeparam name="T7">The type of the seventh arg.</typeparam>
/// <typeparam name="T8">The type of the eighth arg.</typeparam>
/// <param name="arg1">The first arg.</param>
/// <param name="arg2">The second arg.</param>
/// <param name="arg3">The third arg.</param>
/// <param name="arg4">The fourth arg.</param>
/// <param name="arg5">The fifth arg.</param>
/// <param name="arg6">The sixth arg.</param>
/// <param name="arg7">The seventh arg.</param>
/// <param name="arg8">The eighth arg.</param>
internal readonly struct MessageArgs<T1, T2, T3, T4, T5, T6, T7, T8>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8) : IMessageArgs
{
    public int Count => 8;

    public object? this[int index]
    {
        get
        {
            Assumed.InRange(index, 0, Count - 1);
            return index switch
            {
                0 => arg1,
                1 => arg2,
                2 => arg3,
                3 => arg4,
                4 => arg5,
                5 => arg6,
                6 => arg7,
                7 => arg8,
                _ => Assumed.Unreachable<object?>(),
            };
        }
    }

    public string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
        => MessageFormatter.Format(format, [arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8]);
}
