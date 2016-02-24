// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Testing.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Tools.Test
{
    public class ReportingChannel : IReportingChannel
    {
        public static ReportingChannel ListenOn(int port)
        {
            // This fixes the mono incompatibility but ties it to ipv4 connections
            using (var listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                listenSocket.Listen(10);

                var socket = listenSocket.Accept();

                return new ReportingChannel(socket);
            }
        }

        private readonly BinaryWriter _writer;
        private readonly BinaryReader _reader;

        private ReportingChannel(Socket socket)
        {
            Socket = socket;

            var stream = new NetworkStream(Socket);
            _writer = new BinaryWriter(stream);
            _reader = new BinaryReader(stream);

            // Read incoming messages on the background thread
            new Thread(ReadMessages) { IsBackground = true }.Start();
        }

        public event EventHandler<Message> MessageReceived;

        public Socket Socket { get; private set; }

        public int Port => ((IPEndPoint) Socket.LocalEndPoint).Port;

        public void Send(Message message)
        {
            lock (_writer)
            {
                try
                {
                    TestHostTracing.Source.TraceEvent(
                        TraceEventType.Verbose,
                        0,
                        "[ReportingChannel]: Send({0})",
                        message);

                    _writer.Write(JsonConvert.SerializeObject(message));
                }
                catch (Exception ex)
                {
                    TestHostTracing.Source.TraceEvent(
                        TraceEventType.Error,
                        0,
                        "[ReportingChannel]: Error sending {0}",
                        ex);
                    throw;
                }
            }
        }

        public void SendError(string error)
        {
            Send(new Message()
            {
                MessageType = "Error",
                Payload = JToken.FromObject(new ErrorMessage()
                {
                    Message = error,
                }),
            });
        }

        public void SendError(Exception ex)
        {
            SendError(ex.ToString());
        }

        private void ReadMessages()
        {
            while (true)
            {
                try
                {
                    var message = JsonConvert.DeserializeObject<Message>(_reader.ReadString());

                    MessageReceived?.Invoke(this, message);
                }
                catch (Exception ex)
                {
                    TestHostTracing.Source.TraceEvent(
                        TraceEventType.Error,
                        0,
                        "[ReportingChannel]: Waiting for message failed {0}",
                        ex);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            Socket.Dispose();
        }
    }
}