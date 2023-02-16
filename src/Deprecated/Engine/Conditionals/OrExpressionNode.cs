// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Performs logical OR on children
    /// Does not update conditioned properties table
    /// </summary>
    internal sealed class OrExpressionNode : OperatorExpressionNode
    {
        /// <summary>
        /// Evaluate as boolean
        /// </summary>
        internal override bool BoolEvaluate(ConditionEvaluationState state)
        {
            ProjectErrorUtilities.VerifyThrowInvalidProject
                    (LeftChild.CanBoolEvaluate(state),
                     state.conditionAttribute,
                     "ExpressionDoesNotEvaluateToBoolean",
                     LeftChild.GetUnexpandedValue(state),
                     LeftChild.GetExpandedValue(state),
                     state.parsedCondition);

            if (LeftChild.BoolEvaluate(state))
            {
                // Short circuit
                return true;
            }
            else
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject
                    (RightChild.CanBoolEvaluate(state),
                     state.conditionAttribute,
                     "ExpressionDoesNotEvaluateToBoolean",
                     RightChild.GetUnexpandedValue(state),
                     RightChild.GetExpandedValue(state),
                     state.parsedCondition);

                return RightChild.BoolEvaluate(state);
            }
        }

        #region REMOVE_COMPAT_WARNING
        private bool possibleOrCollision = true;
        internal override bool PossibleOrCollision
        {
            set { this.possibleOrCollision = value; }
            get { return this.possibleOrCollision; }
        }
        #endregion
    }
}
