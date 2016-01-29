// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.DotNet.ProjectModel.Server;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ProjectModel.Server.Tests
{
    public class DthTestServer : IDisposable
    {
        private readonly ProjectModelServerCommand _program;
        private readonly Thread _thread;

        public DthTestServer(ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;

            Port = FindFreePort();
            HostId = Guid.NewGuid().ToString();

            _program = new ProjectModelServerCommand(Port, HostId, LoggerFactory);

            _thread = new Thread(() => { _program.OpenChannel(); }) { IsBackground = true };
            _thread.Start();
        }

        public string HostId { get; }

        public int Port { get; }

        public ILoggerFactory LoggerFactory { get; }

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
