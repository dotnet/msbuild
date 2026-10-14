// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.Evaluation;

internal readonly record struct NodeStateLayout(int StringSlotCount, int ComparisonSlotCount);

internal readonly record struct ParsedCondition(GenericExpressionNode Root, NodeStateLayout StateLayout)
{
    internal static ParsedCondition Parse(
        string condition,
        ParserOptions options,
        ElementLocation elementLocation,
        LoggingContext? loggingContext)
    {
        Parser conditionParser = new();

        #region REMOVE_COMPAT_WARNING
        conditionParser.LoggingServices = loggingContext?.LoggingService;
        conditionParser.LogBuildEventContext = loggingContext?.BuildEventContext ?? BuildEventContext.Invalid;
        #endregion

        return conditionParser.ParseForEvaluation(condition, options, elementLocation);
    }

    internal bool Evaluate(ConditionEvaluator.IConditionEvaluationState state)
    {
        state.NodeStates.Initialize(StateLayout);
        try
        {
            return Root.EvaluateCore(state);
        }
        finally
        {
            state.NodeStates.Release();
        }
    }
}
