// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Node representing a string
    /// </summary>
    internal sealed class StringExpressionNode : OperandExpressionNode
    {
        private string value;
        private string cachedExpandedValue;

        internal StringExpressionNode(string value)
        {
            this.value = value;
            this.cachedExpandedValue = null;
        }

        /// <summary>
        /// Evaluate as boolean
        /// </summary>
        internal override bool BoolEvaluate(ConditionEvaluationState state)
        {
            return ConversionUtilities.ConvertStringToBool(GetExpandedValue(state));
        }
        
        /// <summary>
        /// Evaluate as numeric
        /// </summary>
        internal override double NumericEvaluate(ConditionEvaluationState state)
        {
            return ConversionUtilities.ConvertDecimalOrHexToDouble(GetExpandedValue(state));
        }

        internal override bool CanBoolEvaluate(ConditionEvaluationState state)
        {
            return ConversionUtilities.CanConvertStringToBool(GetExpandedValue(state));
        }

        internal override bool CanNumericEvaluate(ConditionEvaluationState state)
        {
            return ConversionUtilities.ValidDecimalOrHexNumber(GetExpandedValue(state));
        }

        /// <summary>
        /// Value before any item and property expressions are expanded
        /// </summary>
        /// <returns></returns>
        internal override string GetUnexpandedValue(ConditionEvaluationState state)
        {
            return value;
        }

        /// <summary>
        /// Value after any item and property expressions are expanded
        /// </summary>
        /// <returns></returns>
        internal override string GetExpandedValue(ConditionEvaluationState state)
        {
            if (cachedExpandedValue == null)
            {
                cachedExpandedValue = state.expanderToUse.ExpandAllIntoString(value, state.conditionAttribute);
            }

            return cachedExpandedValue;
        }

        /// <summary>
        /// If any expression nodes cache any state for the duration of evaluation, 
        /// now's the time to clean it up
        /// </summary>
        internal override void ResetState()
        {
            cachedExpandedValue = null;
        }
    }
}
