// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Compares for left >= right
    /// </summary>
    internal sealed class GreaterThanOrEqualExpressionNode : NumericComparisonExpressionNode
    {
        /// <summary>
        /// Compare numerically
        /// </summary>
        protected override bool Compare(double left, double right)
        {
            return left >= right;
        }
    }
}
