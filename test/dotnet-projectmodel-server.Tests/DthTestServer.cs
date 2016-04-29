// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Microsoft.DotNet.ProjectModel.Server.Tests
{
    public class DthTestServer : IDisposable
    {
        private readonly ProjectModelServerCommand _program;
        private readonly Thread _thread;

        public DthTestServer()
        {
            Port = FindFreePort();
            HostId = Guid.NewGuid().ToString();

            _program = new ProjectModelServerCommand(Port, HostId);

            _thread = new Thread(() => { _program.OpenChannel(); }) { IsBackground = true };
            _thread.Start();
        }

        public string HostId { get; }

        public int Port { get; }

        public void Dispose()
        {
            try
            {
                _program.Shutdown();
            }
            catch (InvalidOperationException)
            {
                // swallow the exception if the process had been terminated.
            }
        }

        private static int FindFreePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }
    }
}
