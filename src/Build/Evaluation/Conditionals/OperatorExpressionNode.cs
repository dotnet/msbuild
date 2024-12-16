﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;

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

        #region REMOVE_COMPAT_WARNING
        internal override bool DetectAnd()
        {
            // Read the state of the current node
            bool detectedAnd = this.PossibleAndCollision;
            // Reset the flags on the current node
            this.PossibleAndCollision = false;
            // Process the children of the node if preset
            bool detectAndRChild = false;
            bool detectAndLChild = false;
            if (RightChild != null)
            {
                detectAndRChild = RightChild.DetectAnd();
            }
            if (LeftChild != null)
            {
                detectAndLChild = LeftChild.DetectAnd();
            }
            return detectedAnd || detectAndRChild || detectAndLChild;
        }

        internal override bool DetectOr()
        {
            // Read the state of the current node
            bool detectedOr = this.PossibleOrCollision;
            // Reset the flags on the current node
            this.PossibleOrCollision = false;
            // Process the children of the node if preset
            bool detectOrRChild = false;
            bool detectOrLChild = false;
            if (RightChild != null)
            {
                detectOrRChild = RightChild.DetectOr();
            }
            if (LeftChild != null)
            {
                detectOrLChild = LeftChild.DetectOr();
            }
            return detectedOr || detectOrRChild || detectOrLChild;
        }
        #endregion
    }
}
