// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Framework;

// TODO: this should be unified with Shared\ErrorUtilities.cs, but it is hard to untangle everything
//       because some of the errors there will use localized resources from different assemblies,
//       which won't be referenceable in Framework.

internal static class FrameworkErrorUtilities
{
    private static readonly bool s_enableMSBuildDebugTracing = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDENABLEDEBUGTRACING"));

    public static void DebugTraceMessage(string category, string message)
    {
        if (s_enableMSBuildDebugTracing)
        {
            Trace.WriteLine(message, category);
        }
    }

    public static void DebugTraceMessage(string category, ref DebugTraceInterpolatedStringHandler handler)
    {
        if (s_enableMSBuildDebugTracing)
        {
            Trace.WriteLine(handler.GetFormattedText(), category);
        }
    }

    /// <summary>
    ///  Interpolated string handler used by <see cref="DebugTraceMessage(string, ref DebugTraceInterpolatedStringHandler)"/>
    ///  to defer string formatting unless tracing is enabled.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct DebugTraceInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public DebugTraceInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            isEnabled = s_enableMSBuildDebugTracing;
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
}
