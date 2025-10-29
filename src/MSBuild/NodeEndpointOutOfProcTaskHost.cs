// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

#nullable disable

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
    /// </summary>
    internal class NodeEndpointOutOfProcTaskHost : NodeEndpointOutOfProcBase
    {
        internal bool _nodeReuse;
        
        #region Constructors and Factories

        /// <summary>
        /// Instantiates an endpoint to act as a client
        /// </summary>
        internal NodeEndpointOutOfProcTaskHost(bool nodeReuse)
        {
            _nodeReuse = nodeReuse;
            InternalConstruct();
        }

        #endregion // Constructors and Factories

        /// <summary>
        /// Returns the host handshake for this node endpoint
        /// </summary>
        protected override Handshake GetHandshake() =>
            new(CommunicationsUtilities.GetHandshakeOptions(taskHost: true, taskHostParameters: TaskHostParameters.Empty, nodeReuse: _nodeReuse));
    }
}
