// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;

#nullable enable

namespace Microsoft.Build.Evaluation;

internal sealed class CachedCondition
{
    private ParsedCondition _condition;
    private bool _initialized;
    private object? _initializationLock;

    internal ParsedCondition GetOrParse(
        string condition,
        ParserOptions options,
        ElementLocation elementLocation,
        LoggingContext? loggingContext)
    {
        if (Volatile.Read(ref _initialized))
        {
            return _condition;
        }

        return Initialize(condition, options, elementLocation, loggingContext);
    }

    private ParsedCondition Initialize(
        string condition,
        ParserOptions options,
        ElementLocation elementLocation,
        LoggingContext? loggingContext) =>
        // Unlike Lazy<T>, failed initialization is retried with the current caller's location.
        LazyInitializer.EnsureInitialized(
            ref _condition,
            ref _initialized,
            ref _initializationLock,
            () => ParsedCondition.Parse(condition, options, elementLocation, loggingContext));
}
