// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Testing.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Tools.Test
{
    public abstract class ReportingChannel : IReportingChannel
    {
        private BinaryWriter _writer;
        private BinaryReader _reader;

        protected ReportingChannel(Socket connectSocket, int port)
        {
            ConnectSocket = connectSocket;
            Port = port;
        }

        protected Socket Socket { get; set; }

        public event EventHandler<Message> MessageReceived;

        public Socket ConnectSocket { get; }

        public int Port { get; }

        public abstract void Connect();

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

        protected void StartReadingMessages()
        {
            var stream = new NetworkStream(Socket);
            _writer = new BinaryWriter(stream);
            _reader = new BinaryReader(stream);

            // Read incoming messages on the background thread
            new Thread(ReadMessages) { IsBackground = true }.Start();
        }

        private void ReadMessages()
        {
            while (true)
            {
                try
                {
                    var rawMessage = _reader.ReadString();
                    var message = JsonConvert.DeserializeObject<Message>(rawMessage);

                    MessageReceived?.Invoke(this, message);

                    if (ShouldStopListening(message))
                    {
                        break;
                    }
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

        private static bool ShouldStopListening(Message message)
        {
            return message.MessageType == TestMessageTypes.TestRunnerTestCompleted ||
                message.MessageType == TestMessageTypes.TestSessionTerminate;
        }

        public void Dispose()
        {
            Socket?.Dispose();
        }
    }
}