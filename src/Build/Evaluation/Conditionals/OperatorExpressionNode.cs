// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Base class for nodes that are operators (have children in the parse tree)
    /// </summary>
    internal abstract class OperatorExpressionNode : GenericExpressionNode
    {
        internal override bool TryBoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, out bool result)
        {
            result = BoolEvaluate(state);
            return true;
        }

        internal abstract bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state);

        internal override bool TryNumericEvaluate(ConditionEvaluator.IConditionEvaluationState state, out double result)
        {
            result = default;
            return false;
        }

        internal override bool TryVersionEvaluate(ConditionEvaluator.IConditionEvaluationState state, out Version result)
        {
            result = default;
            return false;
        }

        /// <summary>
        /// Value after any item and property expressions are expanded
        /// </summary>
        /// <returns></returns>
        internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        {
            return null;
        }

        /// <inheritdoc cref="GenericExpressionNode"/>
        internal override bool IsUnexpandedValueEmpty()
            => (LeftChild?.IsUnexpandedValueEmpty() ?? true) && (RightChild?.IsUnexpandedValueEmpty() ?? true);

        /// <summary>
        /// Value before any item and property expressions are expanded
        /// </summary>
        /// <returns></returns>
        internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        {
            return null;
        }

        /// <summary>
        /// If any expression nodes cache any state for the duration of evaluation,
        /// now's the time to clean it up
        /// </summary>
        /// <remarks>Used by the Wave 18.14 opt-out path.</remarks>
        internal override void ResetState()
        {
            LeftChild?.ResetState();
            
            RightChild?.ResetState();
        }

        /// <summary>
        /// Storage for the left child
        /// </summary>
        internal GenericExpressionNode LeftChild { set; get; }

        /// <summary>
        /// Storage for the right child
        /// </summary>
        internal GenericExpressionNode RightChild { set; get; }
    }
}
