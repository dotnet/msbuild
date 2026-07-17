// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.IO;
using System.IO.Pipes;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.Coordinator;

internal sealed partial class CoordinatorServer
{
    /// <summary>
    ///  Tracks an accepted MSBuild client alongside its <see cref="BuildGrant"/>.
    /// </summary>
    private sealed class ConnectedClient : IDisposable
    {
        private readonly NamedPipeServerStream _pipeStream;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;

        /// <summary>
        ///  Gets the unique identifier for this connection, assigned by the client during handshake.
        /// </summary>
        public Guid ConnectionId { get; }

        /// <summary>
        ///  Gets the process ID of the connected MSBuild client.
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        ///  Gets the capabilities advertised by the client during handshake.
        /// </summary>
        public ImmutableArray<string> Capabilities { get; }

        /// <summary>
        ///  Gets the build grant associated with this client.
        /// </summary>
        public BuildGrant Grant { get; }

        /// <summary>
        ///  Gets a value indicating whether the pipe is still connected to the client.
        /// </summary>
        public bool IsConnected => _pipeStream.IsConnected;

        /// <summary>
        ///  Creates a connected client by taking ownership of a negotiated connection.
        /// </summary>
        public ConnectedClient(Connection connection, BuildGrant grant)
        {
            (_pipeStream, _reader, _writer) = connection.TransferOwnership();

            ConnectionId = connection.Id;
            ProcessId = connection.ProcessId;
            Capabilities = connection.ClientCapabilities;
            Grant = grant;
        }

        /// <summary>
        ///  Reads the next client message from this connected client.
        /// </summary>
        public ClientMessage ReadClientMessage()
            => _reader.ReadClientMessage();

        /// <summary>
        ///  Writes a server message to this connected client.
        /// </summary>
        public void WriteServerMessage(ServerMessage message)
            => _writer.Write(message);

        public void Dispose()
        {
            try
            {
                _writer.Dispose();
            }
            catch (IOException)
            {
                // The pipe may already be broken if the client disconnected.
            }

            _reader.Dispose();
            _pipeStream.Dispose();
        }
    }
}
