// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Represents a number - evaluates as numeric.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal sealed class NumericExpressionNode : OperandExpressionNode
    {
        private string _value;

        internal NumericExpressionNode(string value)
        {
            _value = value;
        }

        internal override bool TryBoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, out bool result)
        {
            result = default;
            return false;
        }

        internal override bool TryNumericEvaluate(ConditionEvaluator.IConditionEvaluationState state, out double result)
        {
            return ConversionUtilities.TryConvertDecimalOrHexToDouble(_value, out result);
        }

        internal override bool TryVersionEvaluate(ConditionEvaluator.IConditionEvaluationState state, out Version result)
        {
            return Version.TryParse(_value, out result);
        }

        /// <summary>
        /// Get the unexpanded value
        /// </summary>
        internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        {
            return _value;
        }

        /// <summary>
        /// Get the expanded value
        /// </summary>
        internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        {
            return _value;
        }

        /// <summary>
        /// If any expression nodes cache any state for the duration of evaluation, 
        /// now's the time to clean it up
        /// </summary>
        internal override void ResetState()
        {
        }

        internal override string DebuggerDisplay => $"#\"{_value}\")";
    }
}
