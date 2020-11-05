// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using LegacyThreadingData = Microsoft.Build.Execution.LegacyThreadingData;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class NodeEndpointInProc_Tests
    {
        private delegate void EndpointOperationDelegate(NodeEndpointInProc endpoint);

        private class MockHost : IBuildComponentHost, INodePacketFactory
        {
            private DataReceivedContext _dataReceivedContext;
            private AutoResetEvent _dataReceivedEvent;
            private BuildParameters _buildParameters;

            /// <summary>
            /// Retrieves the LegacyThreadingData associated with a particular component host
            /// </summary>
            private LegacyThreadingData _legacyThreadingData;


            public MockHost()
            {
                _buildParameters = new BuildParameters();
                _dataReceivedEvent = new AutoResetEvent(false);
                _legacyThreadingData = new LegacyThreadingData();
            }

            public ILoggingService LoggingService
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            /// <summary>
            /// Retrieves the LegacyThreadingData associated with a particular component host
            /// </summary>
            LegacyThreadingData IBuildComponentHost.LegacyThreadingData
            {
                get
                {
                    return _legacyThreadingData;
                }
            }

            public string Name
            {
                get
                {
                    return "NodeEndpointInProc_Tests.MockHost";
                }
            }

            public BuildParameters BuildParameters
            {
                get
                {
                    return _buildParameters;
                }
            }

            #region IBuildComponentHost Members

            public IBuildComponent GetComponent(BuildComponentType type)
            {
                throw new NotImplementedException();
            }

            public void RegisterFactory(BuildComponentType type, BuildComponentFactoryDelegate factory)
            {
            }

            #endregion

            #region INodePacketFactory Members

            public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
            {
                throw new NotImplementedException();
            }

            public void UnregisterPacketHandler(NodePacketType packetType)
            {
                throw new NotImplementedException();
            }

            public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
            {
                throw new NotImplementedException();
            }

            public void RoutePacket(int nodeId, INodePacket packet)
            {
                _dataReceivedContext = new DataReceivedContext(Thread.CurrentThread, packet);
                _dataReceivedEvent.Set();
            }

            public DataReceivedContext DataReceivedContext
            {
                get { return _dataReceivedContext; }
            }

            public WaitHandle DataReceivedEvent
            {
                get { return _dataReceivedEvent; }
            }

            #endregion
        }
        private class TestPacket : INodePacket
        {
            #region INodePacket Members

            public NodePacketType Type
            {
                get { throw new NotImplementedException(); }
            }

            public void Translate(ITranslator translator)
            {
                throw new NotImplementedException();
            }

            #endregion
        }

        private struct LinkStatusContext
        {
            public readonly Thread thread;
            public readonly LinkStatus status;

            public LinkStatusContext(Thread thread, LinkStatus status)
            {
                this.thread = thread;
                this.status = status;
            }
        }

        private struct DataReceivedContext
        {
            public readonly Thread thread;
            public readonly INodePacket packet;

            public DataReceivedContext(Thread thread, INodePacket packet)
            {
                this.thread = thread;
                this.packet = packet;
            }
        }

        private Dictionary<INodeEndpoint, LinkStatusContext> _linkStatusTable;
        private MockHost _host;

        [Fact]
        public void ConstructionWithValidHost()
        {
            NodeEndpointInProc.EndpointPair endpoints =
                NodeEndpointInProc.CreateInProcEndpoints(
                    NodeEndpointInProc.EndpointMode.Synchronous, _host);

            endpoints =
                NodeEndpointInProc.CreateInProcEndpoints(
                    NodeEndpointInProc.EndpointMode.Asynchronous, _host);
        }

        [Fact]
        public void ConstructionSynchronousWithInvalidHost()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                NodeEndpointInProc.CreateInProcEndpoints(
                    NodeEndpointInProc.EndpointMode.Synchronous, null);
            }
           );
        }

        [Fact]
        public void ConstructionAsynchronousWithInvalidHost()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                NodeEndpointInProc.CreateInProcEndpoints(
                    NodeEndpointInProc.EndpointMode.Asynchronous, null);
            }
           );
        }
        /// <summary>
        /// Verify that the links:
        /// 1. are marked inactive
        /// 2. and that attempting to send data while they are
        /// inactive throws the expected exception.
        /// </summary>
        [Fact]
        public void InactiveLinkTestSynchronous()
        {
            NodeEndpointInProc.EndpointPair endpoints =
                NodeEndpointInProc.CreateInProcEndpoints(
                    NodeEndpointInProc.EndpointMode.Synchronous, _host);

            CallOpOnEndpoints(endpoints, VerifyLinkInactive);
            CallOpOnEndpoints(endpoints, VerifySendDataInvalidOperation);
            CallOpOnEndpoints(endpoints, VerifyDisconnectInvalidOperation);

            // The following should not throw
            endpoints.ManagerEndpoint.Listen(_host);
            endpoints.NodeEndpoint.Connect(_host);
        }

        /// <summary>
        /// Verify that the links are marked inactive and that attempting to send data while they are
        /// inactive throws the expected exception.
        /// </summary>
        [Fact]
        public void InactiveLinkTestAsynchronous()
        {
            NodeEndpointInProc.EndpointPair endpoints =
                NodeEndpointInProc.CreateInProcEndpoints(
                    NodeEndpointInProc.EndpointMode.Asynchronous, _host);

            CallOpOnEndpoints(endpoints, VerifyLinkInactive);
            CallOpOnEndpoints(endpoints, VerifySendDataInvalidOperation);
            CallOpOnEndpoints(endpoints, VerifyDisconnectInvalidOperation);

            // The following should not throw
            endpoints.ManagerEndpoint.Listen(_host);
            endpoints.NodeEndpoint.Connect(_host);

            endpoints.ManagerEndpoint.Disconnect();
        }

        [Fact]
        public void ConnectionTestSynchronous()
        {
            NodeEndpointInProc.EndpointPair endpoints =
                NodeEndpointInProc.CreateInProcEndpoints(
                    NodeEndpointInProc.EndpointMode.Synchronous, _host);

            endpoints.ManagerEndpoint.OnLinkStatusChanged += LinkStatusChanged;
            endpoints.NodeEndpoint.OnLinkStatusChanged += LinkStatusChanged;

            // Call listen.  This shouldn't have any effect on the link statuses.
            endpoints.ManagerEndpoint.Listen(_host);
            CallOpOnEndpoints(endpoints, VerifyLinkInactive);
            // No link status callback should have occurred.
            Assert.False(_linkStatusTable.ContainsKey(endpoints.NodeEndpoint));
            Assert.False(_linkStatusTable.ContainsKey(endpoints.ManagerEndpoint));

            // Now call connect on the node side.  This should activate the link on both ends.
            endpoints.NodeEndpoint.Connect(_host);
            CallOpOnEndpoints(endpoints, VerifyLinkActive);

            // We should have received callbacks informing us of the link change.
            Assert.Equal(LinkStatus.Active, _linkStatusTable[endpoints.NodeEndpoint].status);
            Assert.Equal(LinkStatus.Active, _linkStatusTable[endpoints.ManagerEndpoint].status);
        }

        [Fact]
        public void DisconnectionTestSynchronous()
        {
            DisconnectionTestHelper(NodeEndpointInProc.EndpointMode.Synchronous);
        }

        [Fact]
        public void DisconnectionTestAsynchronous()
        {
            DisconnectionTestHelper(NodeEndpointInProc.EndpointMode.Asynchronous);
        }


        [Fact]
        public void SynchronousData()
        {
            // Create the endpoints
            NodeEndpointInProc.EndpointPair endpoints =
                NodeEndpointInProc.CreateInProcEndpoints(
                    NodeEndpointInProc.EndpointMode.Synchronous, _host);

            // Connect the endpoints
            endpoints.ManagerEndpoint.Listen(_host);
            endpoints.NodeEndpoint.Connect(_host);

            // Create our test packets
            INodePacket managerPacket = new TestPacket();
            INodePacket nodePacket = new TestPacket();

            // Send data from the manager. We expect to receive it from the node endpoint, and it should
            // be on the same thread.
            endpoints.ManagerEndpoint.SendData(managerPacket);
            Assert.Equal(_host.DataReceivedContext.packet, managerPacket);
            Assert.Equal(_host.DataReceivedContext.thread.ManagedThreadId, Thread.CurrentThread.ManagedThreadId);

            // Send data from the node.  We expect to receive it from the manager endpoint, and it should
            // be on the same thread.
            endpoints.NodeEndpoint.SendData(nodePacket);
            Assert.Equal(_host.DataReceivedContext.packet, nodePacket);
            Assert.Equal(_host.DataReceivedContext.thread.ManagedThreadId, Thread.CurrentThread.ManagedThreadId);
        }

        [Fact]
        public void AsynchronousData()
        {
            // Create the endpoints
            NodeEndpointInProc.EndpointPair endpoints =
                NodeEndpointInProc.CreateInProcEndpoints(
                    NodeEndpointInProc.EndpointMode.Asynchronous, _host);

            // Connect the endpoints
            endpoints.ManagerEndpoint.Listen(_host);
            endpoints.NodeEndpoint.Connect(_host);

            // Create our test packets
            INodePacket managerPacket = new TestPacket();
            INodePacket nodePacket = new TestPacket();

            // Send data from the manager. We expect to receive it from the node endpoint, and it should
            // be on the same thread.
            endpoints.ManagerEndpoint.SendData(managerPacket);
            if (!_host.DataReceivedEvent.WaitOne(1000))
            {
                Assert.True(false, "Data not received before timeout expired.");
            }
            Assert.Equal(_host.DataReceivedContext.packet, managerPacket);
            Assert.NotEqual(_host.DataReceivedContext.thread.ManagedThreadId, Thread.CurrentThread.ManagedThreadId);

            // Send data from the node.  We expect to receive it from the manager endpoint, and it should
            // be on the same thread.
            endpoints.NodeEndpoint.SendData(nodePacket);
            if (!_host.DataReceivedEvent.WaitOne(1000))
            {
                Assert.True(false, "Data not received before timeout expired.");
            }
            Assert.Equal(_host.DataReceivedContext.packet, nodePacket);
            Assert.NotEqual(_host.DataReceivedContext.thread.ManagedThreadId, Thread.CurrentThread.ManagedThreadId);

            endpoints.ManagerEndpoint.Disconnect();
        }

        public NodeEndpointInProc_Tests()
        {
            _linkStatusTable = new Dictionary<INodeEndpoint, LinkStatusContext>();
            _host = new MockHost();
        }

        private void CallOpOnEndpoints(NodeEndpointInProc.EndpointPair pair, EndpointOperationDelegate opDelegate)
        {
            opDelegate(pair.NodeEndpoint);
            opDelegate(pair.ManagerEndpoint);
        }

        private void VerifyLinkInactive(NodeEndpointInProc endpoint)
        {
            Assert.Equal(LinkStatus.Inactive, endpoint.LinkStatus); // "Expected LinkStatus to be Inactive"
        }

        private void VerifyLinkActive(NodeEndpointInProc endpoint)
        {
            Assert.Equal(LinkStatus.Active, endpoint.LinkStatus); // "Expected LinkStatus to be Active"
        }

        private void VerifySendDataInvalidOperation(NodeEndpointInProc endpoint)
        {
            bool caught = false;
            try
            {
                endpoint.SendData(new TestPacket());
            }
            catch (InternalErrorException)
            {
                caught = true;
            }

            Assert.True(caught); // "Did not receive InternalErrorException."
        }

        private void VerifyDisconnectInvalidOperation(NodeEndpointInProc endpoint)
        {
            bool caught = false;
            try
            {
                endpoint.Disconnect();
            }
            catch (InternalErrorException)
            {
                caught = true;
            }
            Assert.True(caught); // "Did not receive InternalErrorException."
        }

        private void DisconnectionTestHelper(NodeEndpointInProc.EndpointMode mode)
        {
            NodeEndpointInProc.EndpointPair endpoints = SetupConnection(mode);
            endpoints.ManagerEndpoint.Disconnect();
            VerifyLinksAndCallbacksInactive(endpoints);

            endpoints = SetupConnection(mode);
            endpoints.NodeEndpoint.Disconnect();
            VerifyLinksAndCallbacksInactive(endpoints);
        }

        private void VerifyLinksAndCallbacksInactive(NodeEndpointInProc.EndpointPair endpoints)
        {
            CallOpOnEndpoints(endpoints, VerifyLinkInactive);
            Assert.Equal(LinkStatus.Inactive, _linkStatusTable[endpoints.NodeEndpoint].status);
            Assert.Equal(LinkStatus.Inactive, _linkStatusTable[endpoints.ManagerEndpoint].status);
        }

        private NodeEndpointInProc.EndpointPair SetupConnection(NodeEndpointInProc.EndpointMode mode)
        {
            NodeEndpointInProc.EndpointPair endpoints =
                NodeEndpointInProc.CreateInProcEndpoints(mode, _host);

            endpoints.ManagerEndpoint.OnLinkStatusChanged += LinkStatusChanged;
            endpoints.NodeEndpoint.OnLinkStatusChanged += LinkStatusChanged;

            // Call listen.  This shouldn't have any effect on the link statuses.
            endpoints.ManagerEndpoint.Listen(_host);
            endpoints.NodeEndpoint.Connect(_host);

            return endpoints;
        }

        private void LinkStatusChanged(INodeEndpoint endpoint, LinkStatus status)
        {
            lock (_linkStatusTable)
            {
                _linkStatusTable[endpoint] = new LinkStatusContext(Thread.CurrentThread, status);
            }
        }
    }
}
