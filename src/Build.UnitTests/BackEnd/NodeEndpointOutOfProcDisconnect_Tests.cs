// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests that an out-of-proc node endpoint can be disconnected while it is still waiting for a parent
    /// to connect (for example, a worker stopped by Ctrl+C or a console close before any build reached it).
    /// </summary>
    public sealed class NodeEndpointOutOfProcDisconnect_Tests
    {
        private static readonly TimeSpan DisconnectDeadline = TimeSpan.FromSeconds(15);

        [Fact]
        public void Disconnect_WhileWaitingForParentToConnect_ReturnsPromptly()
        {
            NodeEndpointOutOfProc endpoint = new(enableReuse: false, lowPriority: false);
            endpoint.Listen(new NoOpPacketFactory());

            Task disconnect = Task.Run(() => endpoint.Disconnect());

            // Before the fix, Disconnect joined the packet pump, which stayed blocked waiting for a parent
            // to connect, so this only completed after the full node connection timeout.
            disconnect.Wait(DisconnectDeadline).ShouldBeTrue(
                "Disconnect must not wait for a parent that never connects.");
            endpoint.LinkStatus.ShouldBe(LinkStatus.Inactive);
        }

        private sealed class NoOpPacketFactory : INodePacketFactory
        {
            public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
            {
            }

            public void UnregisterPacketHandler(NodePacketType packetType)
            {
            }

            public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
            {
            }

            public INodePacket DeserializePacket(NodePacketType packetType, ITranslator translator) =>
                throw new NotSupportedException("No packets are expected while the endpoint is waiting for a parent.");

            public void RoutePacket(int nodeId, INodePacket packet)
            {
            }
        }
    }
}

#endif
