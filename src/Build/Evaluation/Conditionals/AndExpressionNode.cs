// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System.Diagnostics;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Performs logical AND on children
    /// Does not update conditioned properties table
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal sealed class AndExpressionNode : OperatorExpressionNode
    {
        /// <summary>
        /// Evaluate as boolean
        /// </summary>
        internal override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state)
        {
            if (!LeftChild.TryBoolEvaluate(state, out bool leftBool))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                     state.ElementLocation,
                     "ExpressionDoesNotEvaluateToBoolean",
                     LeftChild.GetUnexpandedValue(state),
                     LeftChild.GetExpandedValue(state),
                     state.Condition);
            }

            if (!leftBool)
            {
                // Short circuit
                return false;
            }
            else
            {
                if (!RightChild.TryBoolEvaluate(state, out bool rightBool))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                         state.ElementLocation,
                         "ExpressionDoesNotEvaluateToBoolean",
                         RightChild.GetUnexpandedValue(state),
                         RightChild.GetExpandedValue(state),
                         state.Condition);
                }

                return rightBool;
            }
        }

        internal override string DebuggerDisplay => $"(and {LeftChild.DebuggerDisplay} {RightChild.DebuggerDisplay})";

        #region REMOVE_COMPAT_WARNING
        private bool _possibleAndCollision = true;
        internal override bool PossibleAndCollision
        {
            set { _possibleAndCollision = value; }
            get { return _possibleAndCollision; }
        }
        #endregion
    }
}
