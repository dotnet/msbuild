// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Base class for nodes that are operators (have children in the parse tree)
    /// </summary>
    internal abstract class OperatorExpressionNode : GenericExpressionNode
    {
        /// <summary>
        /// Storage for the left and right children of the operator
        /// </summary>
        private GenericExpressionNode leftChild, rightChild;

        /// <summary>
        /// Numeric evaluation is never allowed for operators
        /// </summary>
        internal override double NumericEvaluate(ConditionEvaluationState state)
        {
            // Should be unreachable: all calls check CanNumericEvaluate() first
            ErrorUtilities.VerifyThrow(false, "Cannot numeric evaluate an operator");
            return 0.0D;
        }

        /// <summary>
        /// Whether boolean evaluation is allowed: always allowed for operators
        /// </summary>
        internal override bool CanBoolEvaluate(ConditionEvaluationState state)
        {
            return true;
        }

        /// <summary>
        /// Whether the node can be evaluated as a numeric: by default,
        /// this is not allowed
        /// </summary>
        internal override bool CanNumericEvaluate(ConditionEvaluationState state)
        {
            return false;
        }

        /// <summary>
        /// Value after any item and property expressions are expanded
        /// </summary>
        /// <returns></returns>
        internal override string GetExpandedValue(ConditionEvaluationState state)
        {
            return null;
        }

        /// <summary>
        /// Value before any item and property expressions are expanded
        /// </summary>
        /// <returns></returns>
        internal override string GetUnexpandedValue(ConditionEvaluationState state)
        {
            return null;
        }

        /// <summary>
        /// If any expression nodes cache any state for the duration of evaluation,
        /// now's the time to clean it up
        /// </summary>
        internal override void ResetState()
        {
            leftChild?.ResetState();

            rightChild?.ResetState();
        }

        /// <summary>
        /// Storage for the left child
        /// </summary>
        internal GenericExpressionNode LeftChild
        {
            set { this.leftChild = value; }
            get { return this.leftChild; }
        }

        /// <summary>
        /// Storage for the right child
        /// </summary>
        internal GenericExpressionNode RightChild
        {
            set { this.rightChild = value; }
            get { return this.rightChild; }
        }

        #region REMOVE_COMPAT_WARNING
        internal override bool DetectAnd()
        {
            // Read the state of the current node
            bool detectedAnd = this.PossibleAndCollision;
            // Reset the flags on the current node
            this.PossibleAndCollision = false;
            // Process the children of the node if preset
            bool detectAndRChild = false;
            bool detectAndLChild = false;
            if (RightChild != null)
            {
                detectAndRChild = RightChild.DetectAnd();
            }
            if (LeftChild != null)
            {
                detectAndLChild = LeftChild.DetectAnd();
            }
            return detectedAnd || detectAndRChild || detectAndLChild;
        }

        internal override bool DetectOr()
        {
            // Read the state of the current node
            bool detectedOr = this.PossibleOrCollision;
            // Reset the flags on the current node
            this.PossibleOrCollision = false;
            // Process the children of the node if preset
            bool detectOrRChild = false;
            bool detectOrLChild = false;
            if (RightChild != null)
            {
                detectOrRChild = RightChild.DetectOr();
            }
            if (LeftChild != null)
            {
                detectOrLChild = LeftChild.DetectOr();
            }
            return detectedOr || detectOrRChild || detectOrLChild;
        }
        #endregion
    }
}
