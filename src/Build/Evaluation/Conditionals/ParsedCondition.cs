// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation;

internal readonly record struct NodeStateLayout(int StringSlotCount);

internal sealed record ParsedCondition(GenericExpressionNode Root, NodeStateLayout StateLayout)
{
    internal static ParsedCondition Parse(
        string condition,
        ParserOptions options,
        ElementLocation elementLocation,
        LoggingContext? loggingContext)
    {
        Parser conditionParser = new();
        ParsedCondition parsed = conditionParser.Parse(condition, options, elementLocation, loggingContext);
        conditionParser.LogWarnings(condition, loggingContext);
        return parsed;
    }

    internal bool Evaluate(ConditionEvaluator.IConditionEvaluationState state)
    {
        state.NodeStates.Initialize(StateLayout);
        try
        {
            return Root.Evaluate(state);
        }
        finally
        {
            state.NodeStates.Release();
        }
    }
}
