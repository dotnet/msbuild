// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Performs logical NOT on left child
    /// Does not update conditioned properties table
    /// </summary>
    internal sealed class NotExpressionNode : OperatorExpressionNode
    {
        /// <summary>
        /// Evaluate as boolean
        /// </summary>
        internal override bool BoolEvaluate(ConditionEvaluationState state)
        {
            return !LeftChild.BoolEvaluate(state);
        }

        internal override bool CanBoolEvaluate(ConditionEvaluationState state)
        {
            return LeftChild.CanBoolEvaluate(state);
        }

        /// <summary>
        /// Returns unexpanded value with '!' prepended. Useful for error messages.
        /// </summary>
        internal override string GetUnexpandedValue(ConditionEvaluationState state)
        {
            return "!" + LeftChild.GetUnexpandedValue(state);
        }

        /// <summary>
        /// Returns expanded value with '!' prepended. Useful for error messages.
        /// </summary>
        internal override string GetExpandedValue(ConditionEvaluationState state)
        {
            return "!" + LeftChild.GetExpandedValue(state);
        }
    }
}
