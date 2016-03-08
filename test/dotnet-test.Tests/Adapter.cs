// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Testing.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class Adapter : IDisposable
    {
        private readonly string _startMessage;
        private BinaryWriter _writer;
        private BinaryReader _reader;
        private Socket _socket;
        private Socket _listenSocket;

        public IDictionary<string, List<Message>> Messages { get; }

        public int Port { get; private set; }

        public Adapter(string startMessage)
        {
            _startMessage = startMessage;
            Messages = new Dictionary<string, List<Message>>();

            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);
            _listenSocket.Bind(endpoint);

            Port = ((IPEndPoint)_listenSocket.LocalEndPoint).Port;
        }

        public void Listen()
        {
            var listenThread = new Thread(() =>
            {
                using (_listenSocket)
                {
                    _listenSocket.Listen(1);
                    _socket = _listenSocket.Accept();
                }

                var stream = new NetworkStream(_socket);
                _writer = new BinaryWriter(stream);
                _reader = new BinaryReader(stream);

                ReadMessages();
            })
            {
                IsBackground = true
            };

            listenThread.Start();
        }

        public void Send(string messageType)
        {
            lock (_writer)
            {
                _writer.Write(JsonConvert.SerializeObject(new
                {
                    MessageType = messageType,
                    PayLoad = JToken.FromObject(new
                    {
                    })
                }));
            }
        }

        private void ReadMessages()
        {
            while (true)
            {
                try
                {
                    var message = GetMessage();

                    StoreMessage(message);

                    if (HandleMessage(message))
                    {
                        break;
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        private void StoreMessage(Message message)
        {
            if (!Messages.ContainsKey(message.MessageType))
            {
                Messages.Add(message.MessageType, new List<Message>());
            }

            Messages[message.MessageType].Add(message);
        }

        private bool HandleMessage(Message message)
        {
            if (message.MessageType == "TestSession.Connected")
            {
                Send(_startMessage);
            }

            if (message.MessageType == "TestExecution.TestRunnerProcessStartInfo")
            {
                StartTestRunner(message.Payload.ToObject<TestStartInfo>());
            }

            if (message.MessageType == "TestDiscovery.Completed")
            {
                Send("TestSession.Terminate");
                return true;
            }

            if (message.MessageType == "TestExecution.Completed")
            {
                Send("TestSession.Terminate");
                return true;
            }

            return false;
        }

        private static void StartTestRunner(TestStartInfo testPsiInfo)
        {
            var testPsi = new ProcessStartInfo(testPsiInfo.FileName, testPsiInfo.Arguments);
            testPsi.RedirectStandardOutput = true;
            testPsi.UseShellExecute = false;
            var testProcess = new Process
            {
                StartInfo = testPsi
            };

            var testProcessThread = new Thread(() => { testProcess.Start(); })
            {
                IsBackground = true
            };
            testProcessThread.Start();
        }

        private Message GetMessage()
        {
            var rawMessage = _reader.ReadString();
            Console.WriteLine("\nRECEIVING MESSAGE:");
            Console.WriteLine($"{rawMessage}");
            Console.WriteLine($"==============================\n");

            var message = JsonConvert.DeserializeObject<Message>(rawMessage);
            return message;
        }

        public void Dispose()
        {
            _socket?.Dispose();
        }

        private class TestStartInfo
        {
            public string FileName { get; set; }

            public string Arguments { get; set; }
        }
    }
}