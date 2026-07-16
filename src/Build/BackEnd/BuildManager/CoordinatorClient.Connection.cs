// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.BackEnd;

internal sealed partial class CoordinatorClient
{
    /// <summary>
    ///  Owns a connected coordinator pipe during handshake and grant negotiation.
    /// </summary>
    private sealed class Connection : IDisposable
    {
        private NamedPipeClientStream? _pipeStream;
        private BinaryReader? _reader;
        private BinaryWriter? _writer;

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
            var connection = new Connection(pipeStream);

            if (connection.TryHandshake(processId, output))
            {
                return connection;
            }

            // If the handshake wasn't successful.
            connection.Dispose();
            return null;
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
        public ServerMessage ReadServerMessage()
        {
            Assumed.NotNull(_reader);
            return _reader.ReadServerMessage();
        }

        /// <summary>
        ///  Writes a client message to the coordinator pipe.
        /// </summary>
        public void WriteClientMessage(ClientMessage message)
        {
            Assumed.NotNull(_writer);
            _writer.Write(message);
        }

        /// <summary>
        ///  Transfers ownership of the pipe, reader, and writer to the caller.
        /// </summary>
        public (NamedPipeClientStream PipeStream, BinaryReader Reader, BinaryWriter Writer) TransferOwnership()
        {
            Assumed.NotNull(_pipeStream);
            Assumed.NotNull(_reader);
            Assumed.NotNull(_writer);

            var result = (_pipeStream, _reader, _writer);

            _pipeStream = null;
            _reader = null;
            _writer = null;

            return result;
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _pipeStream?.Dispose();
        }
    }
}
