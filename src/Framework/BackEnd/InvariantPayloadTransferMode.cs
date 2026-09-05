// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// How an invariant payload (e.g. the build process environment or the global properties) is transferred
    /// across a task-host connection. Only meaningful when the negotiated packet version is &gt;= 5.
    /// </summary>
    internal enum InvariantPayloadTransferMode : byte
    {
        /// <summary>
        /// The full payload is on the wire. Used for the first config on a connection or whenever it changed.
        /// </summary>
        Full = 0,

        /// <summary>
        /// The payload matches the connection's baseline, so it is not on the wire and the receiver reconstructs
        /// it from that baseline.
        /// </summary>
        Identical = 1,
    }
}
