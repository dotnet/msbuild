// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Compares for inequality
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal sealed class NotEqualExpressionNode : MultipleComparisonNode
    {
        /// <summary>
        /// Compare numbers
        /// </summary>
        protected override bool Compare(double left, double right)
        {
            return left != right;
        }

        /// <summary>
        /// Compare booleans
        /// </summary>
        protected override bool Compare(bool left, bool right)
        {
            return left != right;
        }

        /// <summary>
        /// Compare strings
        /// </summary>
        protected override bool Compare(string left, string right)
        {
            return !String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        internal override string DebuggerDisplay => $"(!= {LeftChild.DebuggerDisplay} {RightChild.DebuggerDisplay})";
    }
}
