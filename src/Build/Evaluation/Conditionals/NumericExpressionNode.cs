// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.BackEnd.Logging;
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

        internal override bool TryBoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, out bool result, LoggingContext loggingContext = null)
        {
            result = default;
            return false;
        }

        internal override bool TryNumericEvaluate(ConditionEvaluator.IConditionEvaluationState state, out double result, LoggingContext loggingContext = null)
        {
            return ConversionUtilities.TryConvertDecimalOrHexToDouble(_value, out result);
        }

        internal override bool TryVersionEvaluate(ConditionEvaluator.IConditionEvaluationState state, out Version result, LoggingContext loggingContext = null)
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
        internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state, LoggingContext loggingContext = null)
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
