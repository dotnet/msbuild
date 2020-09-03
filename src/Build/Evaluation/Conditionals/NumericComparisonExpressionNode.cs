// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using System.IO;
using System;

using Microsoft.Build.Shared;

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
        internal override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state)
        {
            bool isLeftNum = LeftChild.CanNumericEvaluate(state);
            bool isLeftVersion = LeftChild.CanVersionEvaluate(state);
            bool isRightNum = RightChild.CanNumericEvaluate(state);
            bool isRightVersion = RightChild.CanVersionEvaluate(state);
            bool isNumeric = isLeftNum && isRightNum;
            bool isVersion = isLeftVersion && isRightVersion;
            bool isValidComparison = isNumeric || isVersion || (isLeftNum && isRightVersion) || (isLeftVersion && isRightNum);

            ProjectErrorUtilities.VerifyThrowInvalidProject
                (isValidComparison,
                 state.ElementLocation,
                "ComparisonOnNonNumericExpression",
                 state.Condition,
                 /* helpfully display unexpanded token and expanded result in error message */
                 LeftChild.CanNumericEvaluate(state) ? RightChild.GetUnexpandedValue(state) : LeftChild.GetUnexpandedValue(state),
                 LeftChild.CanNumericEvaluate(state) ? RightChild.GetExpandedValue(state) : LeftChild.GetExpandedValue(state));

            // If the values identify as numeric, make that comparison instead of the Version comparison since numeric has a stricter definition
            if (isNumeric)
            {
                return Compare(LeftChild.NumericEvaluate(state), RightChild.NumericEvaluate(state));
            }
            else if (isVersion)
            {
                return Compare(LeftChild.VersionEvaluate(state), RightChild.VersionEvaluate(state));
            }

            // If the numbers are of a mixed type, call that specific Compare method
            if (isLeftNum && isRightVersion)
            {
                return Compare(LeftChild.NumericEvaluate(state), RightChild.VersionEvaluate(state));
            }
            else if (isLeftVersion && isRightNum)
            {
                return Compare(LeftChild.VersionEvaluate(state), RightChild.NumericEvaluate(state));
            }

            // Throw error here as this code should be unreachable
            ErrorUtilities.ThrowInternalErrorUnreachable();
            return false;
        }
    }
}
