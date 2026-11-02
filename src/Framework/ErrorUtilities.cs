// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Framework;

// TODO: this should be unified with Shared\ErrorUtilities.cs, but it is hard to untangle everything
//       because some of the errors there will use localized resources from different assemblies,
//       which won't be referenceable in Framework.

internal static class FrameworkErrorUtilities
{
    /// <summary>
    /// Helper to throw an InternalErrorException when the specified parameter is not a rooted path.
    /// This should be used ONLY if this would indicate a bug in MSBuild rather than
    /// anything caused by user action.
    /// </summary>
    /// <param name="value">Parameter that should be a rooted path.</param>
    public static void VerifyThrowInternalRooted(string value)
    {
        if (!Path.IsPathRooted(value))
        {
            InternalError.Throw($"{value} unexpectedly not a rooted path");
        }
    }

    /// <summary>
    ///  A utility that verifies the parameters provided to a standard <see cref="ICollection{T}.CopyTo"/> call.
    /// </summary>
    /// <typeparam name="T">The element type of the collection.</typeparam>
    /// <param name="collection">The destination collection to copy into.</param>
    /// <param name="index">The zero-based index in <paramref name="collection"/> at which copying begins.</param>
    /// <param name="requiredCapacity">The number of elements that need to be copied.</param>
    /// <param name="collectionParamName">The name of the <paramref name="collection"/> parameter.</param>
    /// <param name="indexParamName">The name of the <paramref name="index"/> parameter.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="collection"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="index"/> falls outside of the bounds of <paramref name="collection"/>.</exception>
    /// <exception cref="ArgumentException">
    ///  If there is insufficient capacity to copy the collection contents into <paramref name="collection"/>
    ///  when starting at <paramref name="index"/>.
    /// </exception>
    public static void VerifyCollectionCopyToArguments<T>(
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
            throw new ArgumentException(SR.CollectionCopyToFailureProvidedArrayIsTooSmall, collectionParamName);
        }
    }

    /// <summary>
    ///  Throws an <see cref="ArgumentException"/> if the given collection is not null but of zero length.
    /// </summary>
    /// <typeparam name="T">The element type of the collection.</typeparam>
    /// <param name="parameter">The collection to check.</param>
    /// <param name="parameterName">The name of the <paramref name="parameter"/>.</param>
    public static void VerifyThrowArgumentLengthIfNotNull<T>(IReadOnlyCollection<T>? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        if (parameter?.Count == 0)
        {
            ThrowArgumentLength(parameterName);
        }
    }

    /// <summary>
    ///  Throws an <see cref="ArgumentException"/> if the string has zero length, unless it is
    ///  null, in which case no exception is thrown.
    /// </summary>
    /// <param name="parameter">The string to check.</param>
    /// <param name="parameterName">The name of the <paramref name="parameter"/>.</param>
    public static void VerifyThrowArgumentLengthIfNotNull(string? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        if (parameter?.Length == 0)
        {
            ThrowArgumentLength(parameterName);
        }
    }

    [DoesNotReturn]
    private static void ThrowArgumentLength(string? parameterName)
        => throw new ArgumentException(SR.Argument_EmptyString, parameterName);

    /// <summary>
    ///  Throws an <see cref="ArgumentNullException"/> if the given string parameter is null,
    ///  and an <see cref="ArgumentException"/> if it contains invalid path or file characters.
    /// </summary>
    /// <param name="parameter">The path to check.</param>
    /// <param name="parameterName">The name of the <paramref name="parameter"/>.</param>
    public static void VerifyThrowArgumentInvalidPath([NotNull] string? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(parameter, parameterName);

        if (FileUtilities.PathIsInvalid(parameter))
        {
            throw new ArgumentException(SR.FormatParameterCannotHaveInvalidPathChars(parameterName, parameter));
        }
    }
}
