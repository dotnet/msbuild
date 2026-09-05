// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Interface for TaskHost callback packets that require request/response correlation.
    /// Packets implementing this interface can be matched between requests sent from TaskHost
    /// and responses received from the owning worker node.
    /// </summary>
    internal interface ITaskHostCallbackPacket : INodePacket
    {
        /// <summary>
        /// Gets or sets the unique request ID for correlating requests with responses.
        /// This ID is assigned by the TaskHost when sending a request and echoed back
        /// by the owning worker node in the corresponding response.
        /// </summary>
        int RequestId { get; set; }
    }
}
