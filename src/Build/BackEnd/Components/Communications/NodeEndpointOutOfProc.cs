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
        {
            _enableReuse = enableReuse;
            LowPriority = lowPriority;

            // Use hash-based pipe name for fast discovery on Unix.
            // Format: MSBuild-{hash}-{pid} — allows schedulers to find compatible nodes
            // by listing /tmp/MSBuild-{hash}-* instead of probing all dotnet processes.
            string? pipeName = NativeMethodsShared.IsUnixLike
                ? NamedPipeUtil.GetHashBasedPipeName(GetHandshake().ComputeHash())
                : null; // Windows: keep legacy MSBuild{PID} naming
            InternalConstruct(pipeName);
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
