// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Represents a number - evaluates as numeric.
    /// </summary>
    internal sealed class NumericExpressionNode : OperandExpressionNode
    {
        private string value;

        private NumericExpressionNode() { }

        internal NumericExpressionNode(string value)
        {
            this.value = value;
        }

        /// <summary>
        /// Evaluate as boolean
        /// </summary>
        internal override bool BoolEvaluate(ConditionEvaluationState state)
        {
            // Should be unreachable: all calls check CanBoolEvaluate() first
            ErrorUtilities.VerifyThrow(false, "Can't evaluate a numeric expression as boolean.");
            return false;
        }

        /// <summary>
        /// Evaluate as numeric
        /// </summary>
        internal override double NumericEvaluate(ConditionEvaluationState state)
        {
            return ConversionUtilities.ConvertDecimalOrHexToDouble(value);
        }

        /// <summary>
        /// Whether it can be evaluated as a boolean: never allowed for numerics
        /// </summary>
        internal override bool CanBoolEvaluate(ConditionEvaluationState state)
        {
            // Numeric expressions are never allowed to be treated as booleans.
            return false;
        }

        internal override bool CanNumericEvaluate(ConditionEvaluationState state)
        {
            // It is not always possible to numerically evaluate even a numerical expression -
            // for example, it may overflow a double. So check here.
            return ConversionUtilities.ValidDecimalOrHexNumber(value);
        }

        internal override string GetUnexpandedValue(ConditionEvaluationState state)
        {
            return value;
        }

        internal override string GetExpandedValue(ConditionEvaluationState state)
        {
            return value;
        }

        /// <summary>
        /// If any expression nodes cache any state for the duration of evaluation, 
        /// now's the time to clean it up
        /// </summary>
        internal override void ResetState()
        {
        }
    }
}
