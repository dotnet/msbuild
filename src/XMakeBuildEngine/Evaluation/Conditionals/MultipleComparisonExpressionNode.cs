// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Evaluates as boolean and evaluates children as boolean, numeric, or string.
    /// Order in which comparisons are attempted is numeric, boolean, then string.
    /// Updates conditioned properties table.
    /// </summary>
    internal abstract class MultipleComparisonNode : OperatorExpressionNode
    {
        private bool _conditionedPropertiesUpdated = false;

        /// <summary>
        /// Compare numbers
        /// </summary>
        protected abstract bool Compare(double left, double right);

        /// <summary>
        /// Compare booleans
        /// </summary>
        protected abstract bool Compare(bool left, bool right);

        /// <summary>
        /// Compare strings
        /// </summary>
        protected abstract bool Compare(string left, string right);

        /// <summary>
        /// Evaluates as boolean and evaluates children as boolean, numeric, or string.
        /// Order in which comparisons are attempted is numeric, boolean, then string.
        /// Updates conditioned properties table.
        /// </summary>
        internal override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state)
        {
            ProjectErrorUtilities.VerifyThrowInvalidProject
                (LeftChild != null && RightChild != null,
                 state.ElementLocation,
                 "IllFormedCondition",
                 state.Condition);

            // It's sometimes possible to bail out of expansion early if we just need to know whether 
            // the result is empty string.
            // If at least one of the left or the right hand side will evaluate to empty, 
            // and we know which do, then we already have enough information to evaluate this expression.
            // That means we don't have to fully expand a condition like " '@(X)' == '' " 
            // which is a performance advantage if @(X) is a huge item list.
            if (LeftChild.EvaluatesToEmpty(state) || RightChild.EvaluatesToEmpty(state))
            {
                UpdateConditionedProperties(state);

                return Compare(LeftChild.EvaluatesToEmpty(state), RightChild.EvaluatesToEmpty(state));
            }

            if (LeftChild.CanNumericEvaluate(state) && RightChild.CanNumericEvaluate(state))
            {
                return Compare(LeftChild.NumericEvaluate(state), RightChild.NumericEvaluate(state));
            }
            else if (LeftChild.CanBoolEvaluate(state) && RightChild.CanBoolEvaluate(state))
            {
                return Compare(LeftChild.BoolEvaluate(state), RightChild.BoolEvaluate(state));
            }
            else // string comparison
            {
                string leftExpandedValue = LeftChild.GetExpandedValue(state);
                string rightExpandedValue = RightChild.GetExpandedValue(state);

                ProjectErrorUtilities.VerifyThrowInvalidProject
                    (leftExpandedValue != null && rightExpandedValue != null,
                     state.ElementLocation,
                     "IllFormedCondition",
                     state.Condition);

                UpdateConditionedProperties(state);

                return Compare(leftExpandedValue, rightExpandedValue);
            }
        }

        /// <summary>
        /// Reset temporary state
        /// </summary>
        internal override void ResetState()
        {
            base.ResetState();
            _conditionedPropertiesUpdated = false;
        }

        /// <summary>
        /// Updates the conditioned properties table if it hasn't already been done.
        /// </summary>
        private void UpdateConditionedProperties(ConditionEvaluator.IConditionEvaluationState state)
        {
            if (!_conditionedPropertiesUpdated && state.ConditionedPropertiesInProject != null)
            {
                string leftUnexpandedValue = LeftChild.GetUnexpandedValue(state);
                string rightUnexpandedValue = RightChild.GetUnexpandedValue(state);

                if (leftUnexpandedValue != null)
                {
                    ConditionEvaluator.UpdateConditionedPropertiesTable
                        (state.ConditionedPropertiesInProject,
                         leftUnexpandedValue,
                         RightChild.GetExpandedValue(state));
                }

                if (rightUnexpandedValue != null)
                {
                    ConditionEvaluator.UpdateConditionedPropertiesTable
                        (state.ConditionedPropertiesInProject,
                         rightUnexpandedValue,
                         LeftChild.GetExpandedValue(state));
                }

                _conditionedPropertiesUpdated = true;
            }
        }
    }
}
