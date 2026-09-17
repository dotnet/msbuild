// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build;

internal static class InternalError
{
    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> with the specified message.
    ///  This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    /// <param name="message">The error message describing the internal failure.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(string message)
        => throw new InternalErrorException(message);

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> with a message constructed from an
    ///  interpolated string handler that always formats its arguments.
    ///  This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    /// <param name="handler">The interpolated string handler that produces the error message.</param>
    [DoesNotReturn]
    public static void Throw(ref UnconditionalInterpolatedStringHandler handler)
        => Throw(handler.GetFormattedText());

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> with the specified message and inner exception.
    ///  This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    /// <param name="message">The error message describing the internal failure.</param>
    /// <param name="innerException">The exception that caused this internal error.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(string message, Exception innerException)
        => throw new InternalErrorException(message, innerException);

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> with a message constructed from an
    ///  interpolated string handler and an inner exception.
    ///  This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    /// <param name="handler">The interpolated string handler that produces the error message.</param>
    /// <param name="innerException">The exception that caused this internal error.</param>
    [DoesNotReturn]
    public static void Throw(ref UnconditionalInterpolatedStringHandler handler, Exception innerException)
        => Throw(handler.GetFormattedText(), innerException);

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> with the specified message and nominally returns
    ///  a value of type <typeparamref name="T"/>. The return type allows use in expression contexts.
    ///  This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    /// <typeparam name="T">The nominal return type, used to satisfy expression contexts.</typeparam>
    /// <param name="message">The error message describing the internal failure.</param>
    /// <returns>
    ///  Never returns; always throws.
    /// </returns>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T Throw<T>(string message)
        => throw new InternalErrorException(message);

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> with a message constructed from an
    ///  interpolated string handler and nominally returns a value of type <typeparamref name="T"/>.
    ///  The return type allows use in expression contexts.
    ///  This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    /// <typeparam name="T">The nominal return type, used to satisfy expression contexts.</typeparam>
    /// <param name="handler">The interpolated string handler that produces the error message.</param>
    /// <returns>
    ///  Never returns; always throws.
    /// </returns>
    [DoesNotReturn]
    public static T Throw<T>(ref UnconditionalInterpolatedStringHandler handler)
        => throw new InternalErrorException(handler.GetFormattedText());

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> with the specified message and inner exception,
    ///  and nominally returns a value of type <typeparamref name="T"/>. The return type allows use in
    ///  expression contexts.
    ///  This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    /// <typeparam name="T">The nominal return type, used to satisfy expression contexts.</typeparam>
    /// <param name="message">The error message describing the internal failure.</param>
    /// <param name="innerException">The exception that caused this internal error.</param>
    /// <returns>
    ///  Never returns; always throws.
    /// </returns>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T Throw<T>(string message, Exception innerException)
        => throw new InternalErrorException(message, innerException);

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> with a message constructed from an
    ///  interpolated string handler and an inner exception, and nominally returns a value of type
    ///  <typeparamref name="T"/>. The return type allows use in expression contexts.
    ///  This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    /// <typeparam name="T">The nominal return type, used to satisfy expression contexts.</typeparam>
    /// <param name="handler">The interpolated string handler that produces the error message.</param>
    /// <param name="innerException">The exception that caused this internal error.</param>
    /// <returns>
    ///  Never returns; always throws.
    /// </returns>
    [DoesNotReturn]
    public static T Throw<T>(ref UnconditionalInterpolatedStringHandler handler, Exception innerException)
        => throw new InternalErrorException(handler.GetFormattedText(), innerException);
}
