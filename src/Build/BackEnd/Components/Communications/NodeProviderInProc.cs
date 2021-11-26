// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

using BuildParameters = Microsoft.Build.Execution.BuildParameters;
using NodeEngineShutdownReason = Microsoft.Build.Execution.NodeEngineShutdownReason;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// An implementation of a node provider for in-proc nodes.
    /// </summary>
    internal class NodeProviderInProc : INodeProvider, INodePacketFactory, IDisposable
    {
        internal sealed class NodeContext
        {
            /// <summary>
            /// The in-proc node.
            /// </summary>
            public INode _inProcNode;

            /// <summary>
            /// The in-proc node endpoint.
            /// </summary>
            public INodeEndpoint _inProcNodeEndpoint;

            /// <summary>
            /// The packet factory used to route packets from the node.
            /// </summary>
            public INodePacketFactory _packetFactory;

            /// <summary>
            /// The in-proc node thread.
            /// </summary>
            public Thread _inProcNodeThread;

            /// <summary>
            /// Event which is raised when the in-proc endpoint is connected.
            /// </summary>
            public AutoResetEvent _endpointConnectedEvent = new AutoResetEvent(false);
        }

        #region Private Data

        /// <summary>
        /// A mapping of all the nodes managed by this provider.
        /// </summary>
        private Dictionary<int, NodeContext> _nodeContexts;

        /// <summary>
        /// Flag indicating we have disposed.
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// Value used to ensure multiple in-proc nodes which save the operating environment are not created.
        /// </summary>
        private static Semaphore InProcNodeOwningOperatingEnvironment;

        /// <summary>
        /// The component host.
        /// </summary>
        private IBuildComponentHost _componentHost;

        /// <summary>
        /// Check to allow the inproc node to have exclusive ownership of the operating environment
        /// </summary>
        private bool _exclusiveOperatingEnvironment = false;

        #endregion

        #region Constructor
        /// <summary>
        /// Initializes the node provider.
        /// </summary>
        public NodeProviderInProc()
        {
        }

        #endregion

        /// <summary>
        /// Finalizer
        /// </summary>
        ~NodeProviderInProc()
        {
            Dispose(false /* disposing */);
        }

        /// <summary>
        /// Returns the type of nodes managed by this provider.
        /// </summary>
        public NodeProviderType ProviderType
        {
            get { return NodeProviderType.InProc; }
        }

        /// <summary>
        /// Returns the number of nodes available to create on this provider.
        /// </summary>
        public int AvailableNodes
        {
            get
            {
                int maxNodeCount = _componentHost.BuildParameters.MultiThreaded ? _componentHost.BuildParameters.MaxNodeCount : 1;
                return maxNodeCount - _nodeContexts.Count;
            }
        }

        #region IBuildComponent Members

        /// <summary>
        /// Sets the build component host.
        /// </summary>
        /// <param name="host">The component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            _componentHost = host;
            _nodeContexts = new Dictionary<int, NodeContext>();
        }

        /// <summary>
        /// Shuts down this component.
        /// </summary>
        public void ShutdownComponent()
        {
        }

        #endregion

        #region INodeProvider Members

        /// <summary>
        /// Sends data to the specified node.
        /// </summary>
        /// <param name="nodeId">The node to which data should be sent.</param>
        /// <param name="packet">The data to send.</param>
        public void SendData(int nodeId, INodePacket packet)
        {
            ErrorUtilities.VerifyThrowArgumentNull(packet, nameof(packet));

            if (_nodeContexts.TryGetValue(nodeId, out NodeContext nodeContext))
            {
                nodeContext._inProcNodeEndpoint.SendData(packet);
            }
        }

        /// <summary>
        /// Causes all connected nodes to be shut down.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if the nodes should prepare for reuse.</param>
        public void ShutdownConnectedNodes(bool enableReuse)
        {
            foreach (NodeContext nodeContext in _nodeContexts.Values)
            {
                nodeContext._inProcNodeEndpoint.SendData(new NodeBuildComplete(enableReuse));
            }
        }

        /// <summary>
        /// Causes all nodes to be shut down permanently - for InProc nodes it is the same as ShutdownConnectedNodes
        /// with enableReuse = false
        /// </summary>
        public void ShutdownAllNodes()
        {
            ShutdownConnectedNodes(false /* no node reuse */);
        }

        /// <summary>
        /// Requests that a node be created on the specified machine.
        /// </summary>
        /// <param name="nodeId">The id of the node to create.</param>
        /// <param name="factory">The factory to use to create packets from this node.</param>
        /// <param name="configuration">The configuration for the node.</param>
        public bool CreateNode(int nodeId, INodePacketFactory factory, NodeConfiguration configuration)
        {
            // Attempt to get the operating environment semaphore if requested.
            if (_componentHost.BuildParameters.SaveOperatingEnvironment)
            {
                // We can only create additional in-proc nodes if we have decided not to save the operating environment.  This is the global
                // DTAR case in Visual Studio, but other clients might enable this as well under certain special circumstances.

                if (Environment.GetEnvironmentVariable("MSBUILDINPROCENVCHECK") == "1")
                {
                    _exclusiveOperatingEnvironment = true;
                }

                if (_exclusiveOperatingEnvironment)
                {
                    if (InProcNodeOwningOperatingEnvironment == null)
                    {
                        InProcNodeOwningOperatingEnvironment = new Semaphore(1, 1);
                    }

                    if (!InProcNodeOwningOperatingEnvironment.WaitOne(0))
                    {
                        // Can't take the operating environment.
                        return false;
                    }
                }
            }

            // If it doesn't already exist, create it.
            if (!_nodeContexts.ContainsKey(nodeId))
            {
                if (!InstantiateNode(nodeId, factory))
                {
                    return false;
                }
            }

            _nodeContexts[nodeId]._inProcNodeEndpoint.SendData(configuration);

            return true;
        }

        #endregion

        #region INodePacketFactory Members

        /// <summary>
        /// Registers a packet handler.  Not used in the in-proc node.
        /// </summary>
        public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            // Not used
            ErrorUtilities.ThrowInternalErrorUnreachable();
        }

        /// <summary>
        /// Unregisters a packet handler.  Not used in the in-proc node.
        /// </summary>
        public void UnregisterPacketHandler(NodePacketType packetType)
        {
            // Not used
            ErrorUtilities.ThrowInternalErrorUnreachable();
        }

        /// <summary>
        /// Deserializes and routes a packet.  Not used in the in-proc node.
        /// </summary>
        public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            // Not used
            ErrorUtilities.ThrowInternalErrorUnreachable();
        }

        /// <summary>
        /// Routes a packet.
        /// </summary>
        /// <param name="nodeId">The id of the node from which the packet is being routed.</param>
        /// <param name="packet">The packet to route.</param>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            if (!_nodeContexts.TryGetValue(nodeId, out NodeContext nodeContext))
            {
                return;
            }

            INodePacketFactory factory = nodeContext._packetFactory;

            // If this was a shutdown packet, we are done with the node.  Release all context associated with it.  Do this here, rather
            // than after we route the packet, because otherwise callbacks to the NodeManager to determine if we have available nodes
            // will report that the in-proc node is still in use when it has actually shut down.
            if (packet.Type == NodePacketType.NodeShutdown)
            {
                // Release the operating environment semaphore if we were holding it.
                if ((_componentHost.BuildParameters.SaveOperatingEnvironment) &&
                    (InProcNodeOwningOperatingEnvironment != null))
                {
                    InProcNodeOwningOperatingEnvironment.Release();
                    InProcNodeOwningOperatingEnvironment.Dispose();
                    InProcNodeOwningOperatingEnvironment = null;
                }

                if (!_componentHost.BuildParameters.EnableNodeReuse)
                {
                    _nodeContexts.Remove(nodeId);
                }
            }

            // Route the packet back to the NodeManager.
            factory.RoutePacket(nodeId, packet);
        }

        #endregion

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true /* disposing */);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Factory for component creation.
        /// </summary>
        static internal IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrow(type == BuildComponentType.InProcNodeProvider, "Cannot create component of type {0}", type);
            return new NodeProviderInProc();
        }

        #region Private Methods

        /// <summary>
        /// Creates a new in-proc node.
        /// </summary>
        private bool InstantiateNode(int nodeId, INodePacketFactory factory)
        {
            ErrorUtilities.VerifyThrow(!_nodeContexts.ContainsKey(nodeId), "In Proc node already instantiated.");

            NodeEndpointInProc.EndpointPair endpoints = NodeEndpointInProc.CreateInProcEndpoints(NodeEndpointInProc.EndpointMode.Synchronous, _componentHost, nodeId);

            NodeContext nodeContext = new();
            _nodeContexts[nodeId] = nodeContext;

            nodeContext._inProcNodeEndpoint = endpoints.ManagerEndpoint;
            nodeContext._inProcNodeEndpoint.OnLinkStatusChanged += new LinkStatusChangedDelegate(InProcNodeEndpoint_OnLinkStatusChanged);

            nodeContext._packetFactory = factory;
            nodeContext._inProcNode = new InProcNode(_componentHost, endpoints.NodeEndpoint, nodeId);
#if FEATURE_THREAD_CULTURE
            nodeContext._inProcNodeThread = new Thread(() => InProcNodeThreadProc(nodeContext._inProcNode), BuildParameters.ThreadStackSize);
#else
                CultureInfo culture = _componentHost.BuildParameters.Culture;
                CultureInfo uiCulture = _componentHost.BuildParameters.UICulture;
                nodeContext._inProcNodeThread = new Thread(() =>
                {
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = uiCulture;
                    InProcNodeThreadProc(nodeContext._inProcNode);
                });
#endif
            nodeContext._inProcNodeThread.Name = String.Format(CultureInfo.CurrentCulture, "In-proc Node ({0})", _componentHost.Name);
            nodeContext._inProcNodeThread.IsBackground = true;
#if FEATURE_THREAD_CULTURE
            nodeContext._inProcNodeThread.CurrentCulture = _componentHost.BuildParameters.Culture;
            nodeContext._inProcNodeThread.CurrentUICulture = _componentHost.BuildParameters.UICulture;
#endif
            nodeContext._inProcNodeThread.Start();

            nodeContext._inProcNodeEndpoint.Connect(this);

            int connectionTimeout = CommunicationsUtilities.NodeConnectionTimeout;
            bool connected = nodeContext._endpointConnectedEvent.WaitOne(connectionTimeout);
            ErrorUtilities.VerifyThrow(connected, "In-proc node failed to start up within {0}ms", connectionTimeout);
            return true;
        }

        /// <summary>
        /// Thread proc which runs the in-proc node.
        /// </summary>
        private void InProcNodeThreadProc(INode inProcNode)
        {
            Exception e;
            NodeEngineShutdownReason reason = inProcNode.Run(out e);
            InProcNodeShutdown(reason, e);
        }

        /// <summary>
        /// Callback invoked when the link status of the endpoint has changed.
        /// </summary>
        /// <param name="endpoint">The endpoint whose status has changed.</param>
        /// <param name="status">The new link status.</param>
        private void InProcNodeEndpoint_OnLinkStatusChanged(INodeEndpoint endpoint, LinkStatus status)
        {
            if (status == LinkStatus.Active)
            {
                bool foundEndpoint = false;
                foreach (NodeContext nodeContext in _nodeContexts.Values)
                {
                    if (endpoint == nodeContext._inProcNodeEndpoint)
                    {
                        nodeContext._endpointConnectedEvent.Set();
                        foundEndpoint = true;
                    }
                }

                if (!foundEndpoint)
                {
                    // We don't verify this outside of the 'if' because we don't care about the link going down, which will occur
                    // after we have cleared the inProcNodeEndpoint due to shutting down the node.
                    ErrorUtilities.VerifyThrow(foundEndpoint, "Received link status event for a node other than our peer.");
                }
            }
        }

        /// <summary>
        /// Callback invoked when the endpoint shuts down.
        /// </summary>
        /// <param name="reason">The reason the endpoint is shutting down.</param>
        /// <param name="e">Any exception which was raised that caused the endpoint to shut down.</param>
        private void InProcNodeShutdown(NodeEngineShutdownReason reason, Exception e)
        {
            switch (reason)
            {
                case NodeEngineShutdownReason.BuildComplete:
                case NodeEngineShutdownReason.BuildCompleteReuse:
                case NodeEngineShutdownReason.Error:
                    break;

                case NodeEngineShutdownReason.ConnectionFailed:
                    ErrorUtilities.ThrowInternalError("Unexpected shutdown code {0} received.", reason);
                    break;
            }
        }

        /// <summary>
        /// Dispose implementation.
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    foreach (NodeContext nodeContext in _nodeContexts.Values)
                    {
                        if (nodeContext._endpointConnectedEvent != null)
                        {
                            nodeContext._endpointConnectedEvent.Dispose();
                            nodeContext._endpointConnectedEvent = null;
                        }
                    }
                }

                _disposed = true;
            }
        }

        #endregion
    }
}
