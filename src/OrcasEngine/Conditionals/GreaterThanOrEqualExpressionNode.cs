using System.Collections;
using System.Globalization;
using System.IO;
using System;

using Microsoft.Build.BuildEngine.Shared;

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
