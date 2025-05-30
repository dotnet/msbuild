// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Execution
{
    #region Enums
    /// <summary>
    /// Reasons for a node to shutdown.
    /// </summary>
    public enum NodeEngineShutdownReason
    {
        /// <summary>
        /// The BuildManager sent a command instructing the node to terminate.
        /// </summary>
        BuildComplete,

        /// <summary>
        /// The BuildManager sent a command instructing the node to terminate, but to restart for reuse.
        /// </summary>
        BuildCompleteReuse,

        /// <summary>
        /// The communication link failed.
        /// </summary>
        ConnectionFailed,

        /// <summary>
        /// The NodeEngine caught an exception which requires the Node to shut down.
        /// </summary>
        Error,
    }
    #endregion
}
