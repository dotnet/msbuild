// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.BackEnd
{
    using NodeEngineShutdownReason = Execution.NodeEngineShutdownReason;

    #region Delegates
    /// <summary>
    /// Delegate is called when a node shuts down.
    /// </summary>
    /// <param name="reason">The reason for the shutdown</param>
    /// <param name="e">The exception which caused an unexpected shutdown, if any.</param>
    internal delegate void NodeShutdownDelegate(NodeEngineShutdownReason reason, Exception e);
    #endregion

    /// <summary>
    /// This interface is implemented by a build node, and allows the host process to control its execution.
    /// </summary>
    internal interface INode
    {
        #region Methods

        /// <summary>
        /// Runs the Node.  Returns the reason the node shut down.
        /// </summary>
        NodeEngineShutdownReason Run(out Exception shutdownException);

        #endregion
    }
}
