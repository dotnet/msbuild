// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestRunnerReportingChannel : ReportingChannel
    {
        public static ReportingChannel ListenOn(int port)
        {
            // This fixes the mono incompatibility but ties it to ipv4 connections
            var listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            listenSocket.Listen(10);

            return new TestRunnerReportingChannel(listenSocket, ((IPEndPoint)listenSocket.LocalEndPoint));
        }

        private TestRunnerReportingChannel(Socket connectSocket, IPEndPoint ipEndPoint)
            : base(connectSocket, ipEndPoint.Port)
        {
        }

        public override void Connect()
        {
            new Thread(() =>
            {
                using (ConnectSocket)
                {
                    Socket = ConnectSocket.Accept();

                    StartReadingMessages();
                }
            })
            { IsBackground = true }.Start();
        }
    }
}
