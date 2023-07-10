// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Evaluates a numeric comparison, such as less-than, or greater-or-equal-than
    /// Does not update conditioned properties table.
    /// </summary>
    internal abstract class NumericComparisonExpressionNode : OperatorExpressionNode
    {
        /// <summary>
        /// Compare numbers
        /// </summary>
        protected abstract bool Compare(double left, double right);

        /// <summary>
        /// Evaluate as boolean
        /// </summary>
        internal override bool BoolEvaluate(ConditionEvaluationState state)
        {
            ProjectErrorUtilities.VerifyThrowInvalidProject
                (LeftChild.CanNumericEvaluate(state) && RightChild.CanNumericEvaluate(state),
                 state.conditionAttribute,
                "ComparisonOnNonNumericExpression",
                 state.parsedCondition,
                 /* helpfully display unexpanded token and expanded result in error message */
                 LeftChild.CanNumericEvaluate(state) ? RightChild.GetUnexpandedValue(state) : LeftChild.GetUnexpandedValue(state),
                 LeftChild.CanNumericEvaluate(state) ? RightChild.GetExpandedValue(state) : LeftChild.GetExpandedValue(state));

            return Compare(LeftChild.NumericEvaluate(state), RightChild.NumericEvaluate(state));
        }
    }
}
