using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
    /// </summary>
    internal class NodeEndpointRarTaskHost : NodeEndpointOutOfProcBase
    {
        /// <summary>
        /// Instantiates an endpoint to act as a client
        /// </summary>
        /// <param name="pipeName">The name of the pipe to which we should connect.</param>
        internal NodeEndpointRarTaskHost(string pipeName)
        {
            InternalConstruct(pipeName, multiClient: true);
        }

        /// <summary>
        /// Returns the host handshake for this node endpoint
        /// </summary>
        protected override Handshake GetHandshake()
        {
            return new Handshake(CommunicationsUtilities.GetHandshakeOptions(taskHost: true) | HandshakeOptions.RarService);
        }
    }
}
