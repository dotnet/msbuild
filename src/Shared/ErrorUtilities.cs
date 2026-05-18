// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared;

/// <summary>
///  This class contains methods that are useful for error checking and validation.
/// </summary>
internal static class ErrorUtilities
{
    /// <inheritdoc cref="FrameworkErrorUtilities.DebugTraceMessage(string, string)"/>
    public static void DebugTraceMessage(string category, string message)
        => FrameworkErrorUtilities.DebugTraceMessage(category, message);

    /// <inheritdoc cref="FrameworkErrorUtilities.DebugTraceMessage(string, ref FrameworkErrorUtilities.DebugTraceInterpolatedStringHandler)"/>
    public static void DebugTraceMessage(string category, ref FrameworkErrorUtilities.DebugTraceInterpolatedStringHandler handler)
        => FrameworkErrorUtilities.DebugTraceMessage(category, ref handler);

    /// <summary>
    /// Throws InternalErrorException.
    /// Indicates the code path followed should not have been possible.
    /// This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    internal static void ThrowIfTypeDoesNotImplementToString(object param)
    {
#if DEBUG
        // Check it has a real implementation of ToString()
        if (String.Equals(param.GetType().ToString(), param.ToString(), StringComparison.Ordinal))
        {
            InternalError.Throw($"This type does not implement ToString() properly {param.GetType().FullName!}");
        }
#endif
    }

    /// <summary>
    /// Helper to throw an InternalErrorException when a lock on the specified object is not already held.
    /// This should be used ONLY if this would indicate a bug in MSBuild rather than
    /// anything caused by user action.
    /// </summary>
    /// <param name="locker">The object that should already have been used as a lock.</param>
    internal static void VerifyThrowInternalLockHeld(object locker)
    {
        Assumed.True(Monitor.IsEntered(locker), "Lock should already have been taken");
    }

    /// <inheritdoc cref="FrameworkErrorUtilities.VerifyThrowInternalRooted(string)"/>
    internal static void VerifyThrowInternalRooted(string value)
        => FrameworkErrorUtilities.VerifyThrowInternalRooted(value);

    /// <summary>
    /// Throws an InvalidOperationException with the specified resource string
    /// </summary>
    /// <param name="resourceName">Resource to use in the exception</param>
    /// <param name="args">Formatting args.</param>
    [DoesNotReturn]
    internal static void ThrowInvalidOperation(string resourceName, params object?[]? args)
    {
        throw new InvalidOperationException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword(resourceName, args));
    }

    /// <summary>
    /// Throws an InvalidOperationException if the given condition is false.
    /// </summary>
    internal static void VerifyThrowInvalidOperation([DoesNotReturnIf(false)] bool condition, string resourceName)
    {
        ResourceUtilities.VerifyResourceStringExists(resourceName);
        if (!condition)
        {
            ThrowInvalidOperation(resourceName, null);
        }
    }

    /// <summary>
    /// Overload for one string format argument.
    /// </summary>
    internal static void VerifyThrowInvalidOperation([DoesNotReturnIf(false)] bool condition, string resourceName, object arg0)
    {
        ResourceUtilities.VerifyResourceStringExists(resourceName);
        // PERF NOTE: check the condition here instead of pushing it into
        // the ThrowInvalidOperation() method, because that method always
        // allocates memory for its variable array of arguments
        if (!condition)
        {
            ThrowInvalidOperation(resourceName, arg0);
        }
    }

    /// <summary>
    /// Overload for two string format arguments.
    /// </summary>
    internal static void VerifyThrowInvalidOperation([DoesNotReturnIf(false)] bool condition, string resourceName, object arg0, object arg1)
    {
        ResourceUtilities.VerifyResourceStringExists(resourceName);
        // PERF NOTE: check the condition here instead of pushing it into
        // the ThrowInvalidOperation() method, because that method always
        // allocates memory for its variable array of arguments
        if (!condition)
        {
            ThrowInvalidOperation(resourceName, arg0, arg1);
        }
    }

    /// <summary>
    /// Overload for three string format arguments.
    /// </summary>
    internal static void VerifyThrowInvalidOperation([DoesNotReturnIf(false)] bool condition, string resourceName, object arg0, object arg1, object arg2)
    {
        ResourceUtilities.VerifyResourceStringExists(resourceName);
        // PERF NOTE: check the condition here instead of pushing it into
        // the ThrowInvalidOperation() method, because that method always
        // allocates memory for its variable array of arguments
        if (!condition)
        {
            ThrowInvalidOperation(resourceName, arg0, arg1, arg2);
        }
    }

    /// <summary>
    /// Overload for four string format arguments.
    /// </summary>
    internal static void VerifyThrowInvalidOperation([DoesNotReturnIf(false)] bool condition, string resourceName, object arg0, object arg1, object arg2, object arg3)
    {
        ResourceUtilities.VerifyResourceStringExists(resourceName);

        // PERF NOTE: check the condition here instead of pushing it into
        // the ThrowInvalidOperation() method, because that method always
        // allocates memory for its variable array of arguments
        if (!condition)
        {
            ThrowInvalidOperation(resourceName, arg0, arg1, arg2, arg3);
        }
    }

    /// <summary>
    /// Throws an ArgumentException that can include an inner exception.
    ///
    /// PERF WARNING: calling a method that takes a variable number of arguments
    /// is expensive, because memory is allocated for the array of arguments -- do
    /// not call this method repeatedly in performance-critical scenarios
    /// </summary>
    [DoesNotReturn]
    internal static void ThrowArgument(string resourceName, params object?[]? args)
    {
        ThrowArgument(null, resourceName, args);
    }

    /// <summary>
    /// Throws an ArgumentException that can include an inner exception.
    ///
    /// PERF WARNING: calling a method that takes a variable number of arguments
    /// is expensive, because memory is allocated for the array of arguments -- do
    /// not call this method repeatedly in performance-critical scenarios
    /// </summary>
    /// <remarks>
    /// This method is thread-safe.
    /// </remarks>
    /// <param name="innerException">Can be null.</param>
    /// <param name="resourceName"></param>
    /// <param name="args"></param>
    [DoesNotReturn]
    internal static void ThrowArgument(Exception? innerException, string resourceName, params object?[]? args)
    {
        throw new ArgumentException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword(resourceName, args), innerException);
    }

    /// <summary>
    /// Throws an ArgumentException if the given condition is false.
    /// </summary>
    internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, string resourceName)
    {
        VerifyThrowArgument(condition, null, resourceName);
    }

    /// <summary>
    /// Overload for one string format argument.
    /// </summary>
    internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, string resourceName, object arg0)
    {
        VerifyThrowArgument(condition, null, resourceName, arg0);
    }

    /// <summary>
    /// Overload for two string format arguments.
    /// </summary>
    internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, string resourceName, object arg0, object arg1)
    {
        VerifyThrowArgument(condition, null, resourceName, arg0, arg1);
    }

    /// <summary>
    /// Overload for three string format arguments.
    /// </summary>
    internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, string resourceName, object arg0, object arg1, object arg2)
    {
        VerifyThrowArgument(condition, null, resourceName, arg0, arg1, arg2);
    }

    /// <summary>
    /// Overload for four string format arguments.
    /// </summary>
    internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, string resourceName, object arg0, object arg1, object arg2, object arg3)
    {
        VerifyThrowArgument(condition, null, resourceName, arg0, arg1, arg2, arg3);
    }

    /// <summary>
    /// Throws an ArgumentException that includes an inner exception, if
    /// the given condition is false.
    /// </summary>
    /// <param name="condition"></param>
    /// <param name="innerException">Can be null.</param>
    /// <param name="resourceName"></param>
    internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, Exception? innerException, string resourceName)
    {
        ResourceUtilities.VerifyResourceStringExists(resourceName);
        if (!condition)
        {
            ThrowArgument(innerException, resourceName, null);
        }
    }

    /// <summary>
    /// Overload for one string format argument.
    /// </summary>
    internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, Exception? innerException, string resourceName, object arg0)
    {
        ResourceUtilities.VerifyResourceStringExists(resourceName);

        if (!condition)
        {
            ThrowArgument(innerException, resourceName, arg0);
        }
    }

    /// <summary>
    /// Overload for two string format arguments.
    /// </summary>
    internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, Exception? innerException, string resourceName, object arg0, object arg1)
    {
        ResourceUtilities.VerifyResourceStringExists(resourceName);

        if (!condition)
        {
            ThrowArgument(innerException, resourceName, arg0, arg1);
        }
    }

    /// <summary>
    /// Overload for three string format arguments.
    /// </summary>
    internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, Exception? innerException, string resourceName, object arg0, object arg1, object arg2)
    {
        ResourceUtilities.VerifyResourceStringExists(resourceName);

        if (!condition)
        {
            ThrowArgument(innerException, resourceName, arg0, arg1, arg2);
        }
    }

    /// <summary>
    /// Overload for four string format arguments.
    /// </summary>
    internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, Exception? innerException, string resourceName, object arg0, object arg1, object arg2, object arg3)
    {
        ResourceUtilities.VerifyResourceStringExists(resourceName);

        if (!condition)
        {
            ThrowArgument(innerException, resourceName, arg0, arg1, arg2, arg3);
        }
    }

    /// <summary>
    /// Throws an argument out of range exception.
    /// </summary>
    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange(string? parameterName)
    {
        throw new ArgumentOutOfRangeException(parameterName);
    }

    /// <summary>
    /// Throws an ArgumentException if the given collection is not null but of zero length.
    /// </summary>
    internal static void VerifyThrowArgumentLengthIfNotNull<T>([MaybeNull] IReadOnlyCollection<T>? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        if (parameter?.Count == 0)
        {
            ThrowArgumentLength(parameterName);
        }
    }

    [DoesNotReturn]
    private static void ThrowArgumentLength(string? parameterName)
    {
        throw new ArgumentException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("Shared.ParameterCannotHaveZeroLength", parameterName));
    }

    /// <summary>
    /// Throws an ArgumentNullException if the given string parameter is null
    /// and ArgumentException if it has zero length.
    /// </summary>
    internal static void VerifyThrowArgumentInvalidPath([NotNull] string parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(parameter, parameterName);

        if (FileUtilities.PathIsInvalid(parameter))
        {
            ThrowArgument("Shared.ParameterCannotHaveInvalidPathChars", parameterName, parameter);
        }
    }

    /// <summary>
    /// Throws an ArgumentException if the string has zero length, unless it is
    /// null, in which case no exception is thrown.
    /// </summary>
    internal static void VerifyThrowArgumentLengthIfNotNull(string? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        if (parameter?.Length == 0)
        {
            ThrowArgumentLength(parameterName);
        }
    }

    /// <summary>
    /// Throws an ArgumentNullException if the given parameter is null.
    /// </summary>
    internal static void VerifyThrowArgumentNull([NotNull] object? parameter, string? parameterName, string resourceName)
    {
        ResourceUtilities.VerifyResourceStringExists(resourceName);
        if (parameter is null)
        {
            ThrowArgumentNull(parameterName, resourceName);
        }
    }

    [DoesNotReturn]
    private static void ThrowArgumentNull(string? parameterName, string resourceName)
    {
        // Most ArgumentNullException overloads append its own rather clunky multi-line message. So use the one overload that doesn't.
        throw new ArgumentNullException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword(resourceName, parameterName), (Exception?)null);
    }

    /// <summary>
    /// A utility that verifies the parameters provided to a standard <see cref="ICollection{T}.CopyTo"/> call.
    /// </summary>
    /// <typeparam name="T">The element type of the collection.</typeparam>
    /// <param name="collection">The destination collection to copy into.</param>
    /// <param name="index">The zero-based index in <paramref name="collection"/> at which copying begins.</param>
    /// <param name="requiredCapacity">The number of elements that need to be copied.</param>
    /// <param name="collectionParamName">The name of the <paramref name="collection"/> parameter.</param>
    /// <param name="indexParamName">The name of the <paramref name="index"/> parameter.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="collection"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="index"/> falls outside of the bounds of <paramref name="collection"/>.</exception>
    /// <exception cref="ArgumentException">If there is insufficient capacity to copy the collection contents into <paramref name="collection"/>
    /// when starting at <paramref name="index"/>.</exception>
    internal static void VerifyCollectionCopyToArguments<T>(
        [NotNull] ICollection<T>? collection,
        int index,
        int requiredCapacity,
        [CallerArgumentExpression(nameof(collection))] string? collectionParamName = null,
        [CallerArgumentExpression(nameof(index))] string? indexParamName = null)
    {
        ArgumentNullException.ThrowIfNull(collection, collectionParamName);
        ArgumentOutOfRangeException.ThrowIfNegative(index, indexParamName);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, collection.Count, indexParamName);

        int capacity = collection.Count - index;
        if (requiredCapacity > capacity)
        {
            throw new ArgumentException(
                ResourceUtilities.GetResourceString("CollectionCopyToFailureProvidedArrayIsTooSmall"),
                collectionParamName);
        }
    }
}
