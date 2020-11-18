// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Evaluates as boolean and evaluates children as boolean, numeric, or string.
    /// Order in which comparisons are attempted is numeric, boolean, then string.
    /// Updates conditioned properties table.
    /// </summary>
    internal abstract class MultipleComparisonNode : OperatorExpressionNode
    {
        private bool conditionedPropertiesUpdated = false;

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
        internal override bool BoolEvaluate(ConditionEvaluationState state)
        {
            ProjectErrorUtilities.VerifyThrowInvalidProject
                (LeftChild != null && RightChild != null,
                 state.conditionAttribute,
                 "IllFormedCondition",
                 state.parsedCondition);

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
                     state.conditionAttribute, 
                     "IllFormedCondition", 
                     state.parsedCondition);

                if (!conditionedPropertiesUpdated)
                {
                    string leftUnexpandedValue = LeftChild.GetUnexpandedValue(state);
                    string rightUnexpandedValue = RightChild.GetUnexpandedValue(state);

                    if (leftUnexpandedValue != null)
                    {
                        Utilities.UpdateConditionedPropertiesTable
                            (state.conditionedPropertiesInProject, 
                             leftUnexpandedValue, 
                             rightExpandedValue);
                    }

                    if (rightUnexpandedValue != null)
                    {
                        Utilities.UpdateConditionedPropertiesTable
                            (state.conditionedPropertiesInProject, 
                             rightUnexpandedValue, 
                             leftExpandedValue);
                    }

                    conditionedPropertiesUpdated = true;
                }

                return Compare(leftExpandedValue, rightExpandedValue);
            }
        }

        /// <summary>
        /// Reset temporary state
        /// </summary>
        internal override void ResetState()
        {
            base.ResetState();
            conditionedPropertiesUpdated = false;
        }
    }
}
