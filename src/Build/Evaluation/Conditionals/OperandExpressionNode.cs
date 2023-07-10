// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Base class for all nodes that are operands (are leaves in the parse tree)
    /// </summary>
    internal abstract class OperandExpressionNode : GenericExpressionNode
    {
        #region REMOVE_COMPAT_WARNING

        internal override bool DetectAnd()
        {
            return false;
        }

        internal override bool DetectOr()
        {
            return false;
        }
        #endregion

    }
}
