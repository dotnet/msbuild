// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
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
            : this(enableReuse, lowPriority, nodeSlot: null)
        {
        }

        /// <summary>
        /// Instantiates an endpoint for one logical slot in a multi-node worker process.
        /// </summary>
        internal NodeEndpointOutOfProc(bool enableReuse, bool lowPriority, int nodeSlot)
            : this(enableReuse, lowPriority, (int?)nodeSlot)
        {
        }

        private NodeEndpointOutOfProc(bool enableReuse, bool lowPriority, int? nodeSlot)
        {
            _enableReuse = enableReuse;
            LowPriority = lowPriority;

            InternalConstruct(
                nodeSlot.HasValue
                    ? NamedPipeUtil.GetPlatformSpecificPipeName(EnvironmentUtilities.CurrentProcessId, nodeSlot.Value)
                    : null);
        }

        /// <summary>
        /// Returns the host handshake for this node endpoint.
        /// </summary>
        protected override Handshake GetHandshake()
        {
            HandshakeOptions handshakeOptions = CommunicationsUtilities.GetHandshakeOptions(
                taskHost: false,
                taskHostParameters: TaskHostParameters.Empty,
                architectureFlagToSet: XMakeAttributes.GetCurrentMSBuildArchitecture(),
                nodeReuse: _enableReuse,
                lowPriority: LowPriority);
            return new Handshake(handshakeOptions);
        }
    }
}
