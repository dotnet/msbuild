// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
