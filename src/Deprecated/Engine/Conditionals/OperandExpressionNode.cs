// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

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
