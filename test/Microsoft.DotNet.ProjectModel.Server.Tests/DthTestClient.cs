// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Server.Tests
{
    public class DthTestClient : IDisposable
    {
        private readonly string _hostId;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;
        private readonly NetworkStream _networkStream;

        private readonly BlockingCollection<DthMessage> _messageQueue;
        private readonly CancellationTokenSource _readCancellationToken;

        // Keeps track of initialized project contexts
        // REVIEW: This needs to be exposed if we ever create 2 clients in order to simulate how build
        // works in visual studio
        private readonly Dictionary<string, int> _projectContexts = new Dictionary<string, int>();
        private int _nextContextId;

        public DthTestClient(DthTestServer server)
        {
            var socket = new Socket(AddressFamily.InterNetwork,
                                    SocketType.Stream,
                                    ProtocolType.Tcp);

            socket.Connect(new IPEndPoint(IPAddress.Loopback, server.Port));

            _hostId = server.HostId;

            _networkStream = new NetworkStream(socket);
            _reader = new BinaryReader(_networkStream);
            _writer = new BinaryWriter(_networkStream);

            _messageQueue = new BlockingCollection<DthMessage>();

            _readCancellationToken = new CancellationTokenSource();
            Task.Run(() => ReadMessage(_readCancellationToken.Token), _readCancellationToken.Token);
        }

        public void SendPayLoad(Project project, string messageType)
        {
            SendPayLoad(project.ProjectDirectory, messageType);
        }

        public void SendPayLoad(string projectPath, string messageType)
        {
            int contextId;
            if (!_projectContexts.TryGetValue(projectPath, out contextId))
            {
                Assert.True(false, $"Unable to resolve context for {projectPath}");
            }

            SendPayLoad(contextId, messageType);
        }

        public void SendPayLoad(int contextId, string messageType)
        {
            SendPayLoad(contextId, messageType, new { });
        }

        public void SendPayLoad(int contextId, string messageType, object payload)
        {
            lock (_writer)
            {
                var message = new
                {
                    ContextId = contextId,
                    HostId = _hostId,
                    MessageType = messageType,
                    Payload = payload
                };
                _writer.Write(JsonConvert.SerializeObject(message));
            }
        }

        public int Initialize(string projectPath)
        {
            var contextId = _nextContextId++;

            _projectContexts[projectPath] = contextId;
            SendPayLoad(contextId, MessageTypes.Initialize, new { ProjectFolder = projectPath });

            return contextId;
        }

        public int Initialize(string projectPath, int protocolVersion)
        {
            var contextId = _nextContextId++;

            _projectContexts[projectPath] = contextId;
            SendPayLoad(contextId, MessageTypes.Initialize, new { ProjectFolder = projectPath, Version = protocolVersion });

            return contextId;
        }

        public int Initialize(string projectPath, int protocolVersion, string configuration)
        {
            var contextId = _nextContextId++;

            _projectContexts[projectPath] = contextId;
            SendPayLoad(contextId, MessageTypes.Initialize, new { ProjectFolder = projectPath, Version = protocolVersion, Configuration = configuration });

            return contextId;
        }

        public void SetProtocolVersion(int version)
        {
            SendPayLoad(0, MessageTypes.ProtocolVersion, new { Version = version });
        }

        public List<DthMessage> DrainMessage(int count)
        {
            var result = new List<DthMessage>();
            while (count > 0)
            {
                result.Add(GetResponse(timeout: TimeSpan.FromSeconds(10)));
                count--;
            }

            return result;
        }

        public List<DthMessage> DrainAllMessages()
        {
            return DrainAllMessages(TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Read all messages from pipeline till timeout
        /// </summary>
        /// <param name="timeout">The timeout</param>
        /// <returns>All the messages in a list</returns>
        public List<DthMessage> DrainAllMessages(TimeSpan timeout)
        {
            var result = new List<DthMessage>();
            while (true)
            {
                try
                {
                    result.Add(GetResponse(timeout));
                }
                catch (TimeoutException)
                {
                    return result;
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Read messages from pipeline until the first match
        /// </summary>]
        /// <param name="type">A message type</param>
        /// <returns>The first match message</returns>
        public DthMessage DrainTillFirst(string type)
        {
            return DrainTillFirst(type, TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Read messages from pipeline until the first match
        /// </summary>
        /// <param name="type">A message type</param>
        /// <param name="timeout">Timeout for each read</param>
        /// <returns>The first match message</returns>
        public DthMessage DrainTillFirst(string type, TimeSpan timeout)
        {
            while (true)
            {
                var next = GetResponse(timeout);
                if (next.MessageType == type)
                {
                    return next;
                }
            }
        }

        /// <summary>
        /// Read messages from pipeline until the first match
        /// </summary>
        /// <param name="type">A message type</param>
        /// <param name="timeout">Timeout</param>
        /// <param name="leadingMessages">All the messages read before the first match</param>
        /// <returns>The first match</returns>
        public DthMessage DrainTillFirst(string type, TimeSpan timeout, out List<DthMessage> leadingMessages)
        {
            leadingMessages = new List<DthMessage>();
            while (true)
            {
                var next = GetResponse(timeout);
                if (next.MessageType == type)
                {
                    return next;
                }
                else
                {
                    leadingMessages.Add(next);
                }
            }
        }

        public void Dispose()
        {
            _reader.Dispose();
            _writer.Dispose();
            _networkStream.Dispose();
            _readCancellationToken.Cancel();
        }

        private void ReadMessage(CancellationToken cancellationToken)
        {
            while (true)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var content = _reader.ReadString();
                    var message = JsonConvert.DeserializeObject<DthMessage>(content);

                    _messageQueue.Add(message);
                }
                catch (IOException)
                {
                    // swallow
                }
                catch (JsonSerializationException deserializException)
                {
                    throw new InvalidOperationException(
                        $"Fail to deserailze data into {nameof(DthMessage)}.",
                        deserializException);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        private DthMessage GetResponse(TimeSpan timeout)
        {
            DthMessage message;

            if (_messageQueue.TryTake(out message, timeout))
            {
                return message;
            }
            else
            {
                throw new TimeoutException($"Response time out after {timeout.TotalSeconds} seconds.");
            }
        }
    }
}
