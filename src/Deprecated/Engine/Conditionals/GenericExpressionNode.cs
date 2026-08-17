// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Base class for all expression nodes.
    /// </summary>
    internal abstract class GenericExpressionNode
    {
        internal abstract bool CanBoolEvaluate(ConditionEvaluationState state);
        internal abstract bool CanNumericEvaluate(ConditionEvaluationState state);
        internal abstract bool BoolEvaluate(ConditionEvaluationState state);
        internal abstract double NumericEvaluate(ConditionEvaluationState state);

        /// <summary>
        /// Value after any item and property expressions are expanded
        /// </summary>
        /// <returns></returns>
        internal abstract string GetExpandedValue(ConditionEvaluationState state);

        /// <summary>
        /// Value before any item and property expressions are expanded
        /// </summary>
        /// <returns></returns>
        internal abstract string GetUnexpandedValue(ConditionEvaluationState state);

        /// <summary>
        /// If any expression nodes cache any state for the duration of evaluation,
        /// now's the time to clean it up
        /// </summary>
        internal abstract void ResetState();

        /// <summary>
        /// The main evaluate entry point for expression trees
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        internal bool Evaluate(ConditionEvaluationState state)
        {
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                CanBoolEvaluate(state),
                state.conditionAttribute,
                "ConditionNotBooleanDetail",
                state.parsedCondition,
                GetExpandedValue(state));

            return BoolEvaluate(state);
        }

        #region REMOVE_COMPAT_WARNING
        internal virtual bool PossibleAndCollision
        {
            set { /* do nothing */ }
            get { return false; }
        }

        internal virtual bool PossibleOrCollision
        {
            set { /* do nothing */ }
            get { return false; }
        }

        internal bool PotentialAndOrConflict()
        {
            // The values of the functions are assigned to boolean locals
            // in order to force evaluation of the functions even when the
            // first one returns false
            bool detectOr = DetectOr();
            bool detectAnd = DetectAnd();
            return detectOr && detectAnd;
        }

        internal abstract bool DetectOr();
        internal abstract bool DetectAnd();
        #endregion

    }
}
