// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Compares for equality
    /// </summary>
    internal sealed class EqualExpressionNode : MultipleComparisonNode
    {
        /// <summary>
        /// Compare numbers
        /// </summary>
        protected override bool Compare(double left, double right)
        {
            return left == right;
        }

        /// <summary>
        /// Compare booleans
        /// </summary>
        protected override bool Compare(bool left, bool right)
        {
            return left == right;
        }

        /// <summary>
        /// Compare strings
        /// </summary>
        protected override bool Compare(string left, string right)
        {
            return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
