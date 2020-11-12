// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BuildEngine
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
