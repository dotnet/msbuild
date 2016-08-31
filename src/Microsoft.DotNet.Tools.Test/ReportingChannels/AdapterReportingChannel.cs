// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Testing.Abstractions;
using System.Net;
using System.Net.Sockets;

namespace Microsoft.DotNet.Tools.Test
{
    public class AdapterReportingChannel : ReportingChannel
    {
        private readonly IPEndPoint _ipEndPoint;

        public static AdapterReportingChannel ConnectTo(int port)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            var ipEndPoint = new IPEndPoint(IPAddress.Loopback, port);

            return new AdapterReportingChannel(socket, ipEndPoint);
        }

        private AdapterReportingChannel(Socket connectSocket, IPEndPoint ipEndPoint)
            : base(connectSocket, ipEndPoint.Port)
        {
            _ipEndPoint = ipEndPoint;
        }

        public override void Connect()
        {
            Socket = ConnectSocket;

            Socket.Connect(_ipEndPoint);

            StartReadingMessages();

            Send(new Message
            {
                MessageType = TestMessageTypes.TestSessionConnected
            });
        }
    }
}
