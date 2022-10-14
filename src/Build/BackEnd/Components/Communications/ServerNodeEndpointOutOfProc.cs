// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This is an implementation of out-of-proc server node endpoint.
    /// </summary>
    internal sealed class ServerNodeEndpointOutOfProc : NodeEndpointOutOfProcBase
    {
        private readonly Handshake _handshake;

        /// <summary>
        /// Instantiates an endpoint to act as a client
        /// </summary>
        /// <param name="pipeName">The name of the pipe to which we should connect.</param>
        /// <param name="handshake"></param>
        internal ServerNodeEndpointOutOfProc(
            string pipeName,
            Handshake handshake)
        {
            _handshake = handshake;

            InternalConstruct(pipeName);
        }

        /// <summary>
        /// Returns the host handshake for this node endpoint
        /// </summary>
        protected override Handshake GetHandshake()
        {
            return _handshake;
        }
    }
}
