// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Describes the state storage required for each evaluation of a parsed condition.
/// </summary>
/// <param name="StringSlotCount">
/// Number of per-evaluation slots, assigned only to expandable string nodes.
/// Each slot indexes one node's cached expansion.
/// </param>
internal readonly record struct NodeStateLayout(int StringSlotCount);

/// <summary>
/// A reusable parsed condition with a shared expression tree and a private node-state strorage.
/// </summary>
/// <param name="Root">Root of the shared expression tree.</param>
/// <param name="StateLayout">Layout for each evaluation's private node-state storage.</param>
internal sealed record ParsedCondition(GenericExpressionNode Root, NodeStateLayout StateLayout)
{
    /// <summary>
    /// Parses an uncached condition and reports compatibility warnings.
    /// </summary>
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

    /// <summary>
    /// Evaluates the shared tree using state private to this evaluation.
    /// </summary>
    /// <remarks>
    /// Initializes node-state storage from <see cref="StateLayout"/>.
    /// </remarks>
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
