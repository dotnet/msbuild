// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation;

internal sealed class CachedCondition
{
    private ParsedCondition? _condition;

    internal ParsedCondition GetOrParse(
        string condition,
        ParserOptions options,
        ElementLocation elementLocation,
        LoggingContext? loggingContext)
    {
        ParsedCondition? parsed = Volatile.Read(ref _condition);
        return parsed ?? Initialize(condition, options, elementLocation, loggingContext);
    }

    private ParsedCondition Initialize(
        string condition,
        ParserOptions options,
        ElementLocation elementLocation,
        LoggingContext? loggingContext)
    {
        Parser conditionParser = new();
        ParsedCondition parsed = conditionParser.Parse(condition, options, elementLocation, loggingContext);
        ParsedCondition? existing = Interlocked.CompareExchange(ref _condition, parsed, null);
        if (existing is not null)
        {
            return existing;
        }

        conditionParser.LogWarnings(condition, loggingContext);
        return parsed;
    }
}
