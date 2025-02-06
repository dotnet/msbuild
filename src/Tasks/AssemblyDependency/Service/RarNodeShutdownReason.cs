// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Reasons why the RAR node shutdown.
    /// </summary>
    public enum RarNodeShutdownReason
    {
        /// <summary>
        /// The RAR node is already running.
        /// </summary>
        AlreadyRunning,

        /// <summary>
        /// The RAR node timed out waiting for a connection or handshake completion.
        /// </summary>
        ConnectionTimedOut,

        /// <summary>
        /// The RAR node encountered an unrecoverable error.
        /// </summary>
        Error,
    }
}