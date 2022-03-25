// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
    /// </summary>
    internal class ServerNodeEndpointOutOfProc : NodeEndpointOutOfProcBase
    {
        #region Private Data

        private readonly IHandshake _handshake;

        #endregion

        #region Constructors and Factories

        /// <summary>
        /// Instantiates an endpoint to act as a client
        /// </summary>
        /// <param name="pipeName">The name of the pipe to which we should connect.</param>
        /// <param name="handshake"></param>
        internal ServerNodeEndpointOutOfProc(
            string pipeName,
            IHandshake handshake)
        {
            _handshake = handshake;

            InternalConstruct(pipeName);
        }

        #endregion

        /// <summary>
        /// Returns the host handshake for this node endpoint
        /// </summary>
        protected override IHandshake GetHandshake()
        {
            return _handshake;
        }
    }
}
