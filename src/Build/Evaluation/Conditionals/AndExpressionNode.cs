// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;

#nullable disable

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
        internal override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, LoggingContext loggingContext = null)
        {
            if (!LeftChild.TryBoolEvaluate(state, out bool leftBool, loggingContext))
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
                if (!RightChild.TryBoolEvaluate(state, out bool rightBool, loggingContext))
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
