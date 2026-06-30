// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.BackEnd;

internal sealed partial class CoordinatorClient
{
    /// <summary>
    ///  Owns a connected coordinator pipe during handshake and grant negotiation.
    /// </summary>
    private sealed class Connection : IDisposable
    {
        private readonly NamedPipeClientStream _pipeStream;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;
        private readonly LockType _writeLock = new();
        private int _disposed;

        /// <summary>
        ///  Gets the unique identifier sent during the coordinator handshake.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        ///  Gets the capabilities advertised by the coordinator server during handshake.
        /// </summary>
        public ImmutableArray<string> ServerCapabilities { get; private set; }

        private Connection(NamedPipeClientStream pipeStream)
        {
            _pipeStream = pipeStream;
            _reader = new BinaryReader(pipeStream, Encoding.UTF8, leaveOpen: true);
            _writer = new BinaryWriter(pipeStream, Encoding.UTF8, leaveOpen: true);

            Id = Guid.NewGuid();
        }

        /// <summary>
        ///  Creates a handshaken coordinator connection over an already-connected pipe.
        /// </summary>
        /// <returns>
        ///  A connection that owns the pipe, reader, and writer; or <see langword="null"/> if the handshake failed.
        /// </returns>
        public static Connection? TryCreate(NamedPipeClientStream pipeStream, int processId, ICoordinatorDebugOutput output)
        {
            Connection? connection = null;
            try
            {
                connection = new Connection(pipeStream);

                if (connection.TryHandshake(processId, output))
                {
                    Connection result = connection;
                    connection = null;
                    return result;
                }

                // If the handshake wasn't successful.
                return null;
            }
            finally
            {
                connection?.Dispose();
            }
        }

        private bool TryHandshake(int processId, ICoordinatorDebugOutput output)
        {
            output.WriteLine($"CoordinatorClient: Sending handshake (ConnectionId {Id})");
            WriteClientMessage(new ClientHandshakeMessage(Id, processId, capabilities: [Capabilities.NestedGrants]));

            ServerMessage response = ReadServerMessage();

            if (response is ServerHandshakeMessage serverHandshake)
            {
                ServerCapabilities = serverHandshake.Capabilities;
                output.WriteLine($"CoordinatorClient: Handshake complete (server capabilities: [{string.Join(", ", serverHandshake.Capabilities)}])");
                return true;
            }

            if (response is ErrorMessage error)
            {
                output.WriteLine($"CoordinatorClient: Server rejected handshake: {error.Message}");
                return false;
            }

            output.WriteLine($"CoordinatorClient: Unexpected handshake response: {response.GetType().Name}");
            return false;
        }

        /// <summary>
        ///  Reads the next server message from the coordinator pipe.
        /// </summary>
        public ServerMessage ReadServerMessage() => _reader.ReadServerMessage();

        /// <summary>
        ///  Writes a client message to the coordinator pipe.
        /// </summary>
        public void WriteClientMessage(ClientMessage message)
        {
            lock (_writeLock)
            {
                _writer.Write(message);
            }
        }

        /// <summary>
        ///  Checks whether the coordinator server advertised a capability during handshake.
        /// </summary>
        public bool HasServerCapability(string capability) => ServerCapabilities.Contains(capability);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                lock (_writeLock)
                {
                    _writer.Dispose();
                }
            }
            catch (IOException)
            {
                // Flush in BinaryWriter.Dispose can throw if the pipe is already broken.
            }

            _reader.Dispose();
            _pipeStream.Dispose();
        }
    }
}
