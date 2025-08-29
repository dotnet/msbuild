// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    using NodeEngineShutdownReason = Microsoft.Build.Execution.NodeEngineShutdownReason;

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
