// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System.Diagnostics;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Performs logical NOT on left child
    /// Does not update conditioned properties table
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal sealed class NotExpressionNode : OperatorExpressionNode
    {
        /// <summary>
        /// Evaluate as boolean
        /// </summary>
        internal override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state)
        {
            if (!LeftChild.TryBoolEvaluate(state, out bool boolValue))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    state.ElementLocation,
                    "ExpressionDoesNotEvaluateToBoolean",
                    LeftChild.GetUnexpandedValue(state),
                    LeftChild.GetExpandedValue(state),
                    state.Condition);
            }

            return !boolValue;
        }

        /// <summary>
        /// Returns unexpanded value with '!' prepended. Useful for error messages.
        /// </summary>
        internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        {
            return "!" + LeftChild.GetUnexpandedValue(state);
        }

        /// <summary>
        /// Returns expanded value with '!' prepended. Useful for error messages.
        /// </summary>
        internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        {
            return "!" + LeftChild.GetExpandedValue(state);
        }

        internal override string DebuggerDisplay => $"(not {LeftChild.DebuggerDisplay})";
    }
}
