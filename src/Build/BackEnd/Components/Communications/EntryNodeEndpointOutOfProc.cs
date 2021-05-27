// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
    /// </summary>
    internal class EntryNodeEndpointOutOfProc : NodeEndpointOutOfProcBase
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
        internal EntryNodeEndpointOutOfProc(
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
