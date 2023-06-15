// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
    /// </summary>
    internal sealed class NodeEndpointOutOfProc : NodeEndpointOutOfProcBase
    {
        private readonly bool _enableReuse;

        internal bool LowPriority { get; private set; }

        /// <summary>
        /// Instantiates an endpoint to act as a client.
        /// </summary>
        /// <param name="enableReuse">Whether this node may be reused for a later build.</param>
        /// <param name="lowPriority">Whether this node is low priority.</param>
        internal NodeEndpointOutOfProc(bool enableReuse, bool lowPriority)
        {
            _enableReuse = enableReuse;
            LowPriority = lowPriority;

            InternalConstruct();
        }

        /// <summary>
        /// Returns the host handshake for this node endpoint.
        /// </summary>
        protected override Handshake GetHandshake()
        {
            HandshakeOptions handshakeOptions = CommunicationsUtilities.GetHandshakeOptions(
                taskHost: false,
                architectureFlagToSet: XMakeAttributes.GetCurrentMSBuildArchitecture(),
                nodeReuse: _enableReuse,
                lowPriority: LowPriority);
            return new Handshake(handshakeOptions);
        }
    }
}
