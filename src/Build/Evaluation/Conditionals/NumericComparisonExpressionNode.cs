// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation
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
        /// Compare Versions. This is only intended to compare version formats like "A.B.C.D" which can otherwise not be compared numerically
        /// </summary>
        protected abstract bool Compare(Version left, Version right);

        /// <summary>
        /// Compare mixed numbers and Versions
        /// </summary>
        protected abstract bool Compare(Version left, double right);

        /// <summary>
        /// Compare mixed numbers and Versions
        /// </summary>
        protected abstract bool Compare(double left, Version right);

        /// <summary>
        /// Evaluate as boolean
        /// </summary>
        internal override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, LoggingContext loggingContext = null)
        {
            bool isLeftNum = LeftChild.TryNumericEvaluate(state, out double leftNum);
            bool isLeftVersion = LeftChild.TryVersionEvaluate(state, out Version leftVersion);
            bool isRightNum = RightChild.TryNumericEvaluate(state, out double rightNum);
            bool isRightVersion = RightChild.TryVersionEvaluate(state, out Version rightVersion);

            if ((!isLeftNum && !isLeftVersion) || (!isRightNum && !isRightVersion))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    state.ElementLocation,
                    "ComparisonOnNonNumericExpression",
                    state.Condition,
                    /* helpfully display unexpanded token and expanded result in error message */
                    isLeftNum ? RightChild.GetUnexpandedValue(state) : LeftChild.GetUnexpandedValue(state),
                    isLeftNum ? RightChild.GetExpandedValue(state, loggingContext) : LeftChild.GetExpandedValue(state, loggingContext));
            }

            return (isLeftNum, isLeftVersion, isRightNum, isRightVersion) switch
            {
                (true, _, true, _) => Compare(leftNum, rightNum),
                (_, true, _, true) => Compare(leftVersion, rightVersion),
                (true, _, _, true) => Compare(leftNum, rightVersion),
                (_, true, true, _) => Compare(leftVersion, rightNum),

                _ => false
            };
        }
    }
}
