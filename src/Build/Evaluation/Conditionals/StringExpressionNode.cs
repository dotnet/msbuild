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
    /// Node representing a string
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal sealed class StringExpressionNode : OperandExpressionNode
    {
        private string _value;
        private string _cachedExpandedValue;

        /// <summary>
        /// Whether the string potentially has expandable content,
        /// such as a property expression or escaped character.
        /// </summary>
        private bool _expandable;

        internal StringExpressionNode(string value, bool expandable)
        {
            _value = value;
            _expandable = expandable;
        }

        internal override bool TryBoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, out bool result, LoggingContext loggingContext = null)
        {
            return ConversionUtilities.TryConvertStringToBool(GetExpandedValue(state, loggingContext), out result);
        }

        internal override bool TryNumericEvaluate(ConditionEvaluator.IConditionEvaluationState state, out double result, LoggingContext loggingContext = null)
        {
            if (ShouldBeTreatedAsVisualStudioVersion(state, loggingContext))
            {
                result = ConversionUtilities.ConvertDecimalOrHexToDouble(MSBuildConstants.CurrentVisualStudioVersion);
                return true;
            }
            else
            {
                return ConversionUtilities.TryConvertDecimalOrHexToDouble(GetExpandedValue(state, loggingContext), out result);
            }
        }

        internal override bool TryVersionEvaluate(ConditionEvaluator.IConditionEvaluationState state, out Version result, LoggingContext loggingContext = null)
        {
            if (ShouldBeTreatedAsVisualStudioVersion(state, loggingContext))
            {
                result = Version.Parse(MSBuildConstants.CurrentVisualStudioVersion);
                return true;
            }
            else
            {
                return Version.TryParse(GetExpandedValue(state, loggingContext), out result);
            }
        }

        /// <summary>
        /// Returns true if this node evaluates to an empty string,
        /// otherwise false.
        /// It may be cheaper to determine whether an expression will evaluate
        /// to empty than to fully evaluate it.
        /// Implementations should cache the result so that calls after the first are free.
        /// </summary>
        internal override bool EvaluatesToEmpty(ConditionEvaluator.IConditionEvaluationState state, LoggingContext loggingContext = null)
        {
            if (_cachedExpandedValue == null)
            {
                if (_expandable)
                {
                    switch (_value.Length)
                    {
                        case 0:
                            _cachedExpandedValue = String.Empty;
                            return true;
                        // If the length is 1 or 2, it can't possibly be a property, item, or metadata, and it isn't empty.
                        case 1:
                        case 2:
                            _cachedExpandedValue = _value;
                            return false;
                        default:
                            if (_value[1] != '(' || (_value[0] != '$' && _value[0] != '%' && _value[0] != '@') || _value[_value.Length - 1] != ')')
                            {
                                // This isn't just a property, item, or metadata value, and it isn't empty.
                                return false;
                            }
                            break;
                    }

                    string expandBreakEarly = state.ExpandIntoStringBreakEarly(_value, loggingContext);

                    if (expandBreakEarly == null)
                    {
                        // It broke early: we can't store the value, we just
                        // know it's non empty
                        return false;
                    }

                    // It didn't break early, the result is accurate,
                    // so store it so the work isn't done again.
                    _cachedExpandedValue = expandBreakEarly;
                }
                else
                {
                    _cachedExpandedValue = _value;
                }
            }

            return _cachedExpandedValue.Length == 0;
        }


        /// <summary>
        /// Value before any item and property expressions are expanded
        /// </summary>
        /// <returns></returns>
        internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        {
            return _value;
        }

        /// <summary>
        /// Value after any item and property expressions are expanded
        /// </summary>
        /// <returns></returns>
        internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state, LoggingContext loggingContext = null)
        {
            if (_cachedExpandedValue == null)
            {
                if (_expandable)
                {
                    _cachedExpandedValue = state.ExpandIntoString(_value, loggingContext);
                }
                else
                {
                    _cachedExpandedValue = _value;
                }
            }

            return _cachedExpandedValue;
        }

        /// <summary>
        /// If any expression nodes cache any state for the duration of evaluation,
        /// now's the time to clean it up
        /// </summary>
        internal override void ResetState()
        {
            _cachedExpandedValue = null;
            _shouldBeTreatedAsVisualStudioVersion = null;
        }

        private bool? _shouldBeTreatedAsVisualStudioVersion = null;

        /// <summary>
        /// Should this node be treated as an expansion of VisualStudioVersion, rather than
        /// its literal meaning?
        /// </summary>
        /// <remarks>
        /// Needed to provide a compat shim for numeric/version comparisons
        /// on MSBuildToolsVersion, which were fine when it was a number
        /// but now cause the project to throw InvalidProjectException when
        /// ToolsVersion is "Current". https://github.com/dotnet/msbuild/issues/4150
        /// </remarks>
        private bool ShouldBeTreatedAsVisualStudioVersion(ConditionEvaluator.IConditionEvaluationState state, LoggingContext loggingContext = null)
        {
            if (!_shouldBeTreatedAsVisualStudioVersion.HasValue)
            {
                // Treat specially if the node would expand to "Current".

                // Do this check first, because if it's not (common) we can early-out and the next
                // expansion will be cheap because this will populate the cached expanded value.
                if (string.Equals(GetExpandedValue(state, loggingContext), MSBuildConstants.CurrentToolsVersion, StringComparison.Ordinal))
                {
                    // and it is just an expansion of MSBuildToolsVersion
                    _shouldBeTreatedAsVisualStudioVersion = string.Equals(_value, "$(MSBuildToolsVersion)", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    _shouldBeTreatedAsVisualStudioVersion = false;
                }
            }

            return _shouldBeTreatedAsVisualStudioVersion.Value;
        }

        internal override string DebuggerDisplay => $"\"{_value}\"";
    }
}
