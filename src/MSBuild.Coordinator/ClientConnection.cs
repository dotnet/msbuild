// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.IO;
using System.IO.Pipes;

namespace Microsoft.Build.Coordinator;

/// <summary>
///  Tracks the pipe connection for a single MSBuild client alongside its <see cref="BuildGrant"/>.
/// </summary>
internal sealed class ClientConnection : IDisposable
{
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
    ///  Gets the build grant associated with this connection.
    /// </summary>
    public BuildGrant Grant { get; }

    /// <summary>
    ///  Gets the named pipe stream connected to the client.
    /// </summary>
    public NamedPipeServerStream PipeStream { get; }

    /// <summary>
    ///  Gets a reader for deserializing client messages from the pipe.
    /// </summary>
    public BinaryReader Reader { get; }

    /// <summary>
    ///  Gets a writer for serializing server messages to the pipe.
    /// </summary>
    public BinaryWriter Writer { get; }

    /// <summary>
    ///  Creates a new client connection wrapping the given identity, grant, and pipe stream.
    /// </summary>
    /// <param name="connectionId">The unique identifier for this connection.</param>
    /// <param name="processId">The process ID of the connected client.</param>
    /// <param name="capabilities">The capabilities advertised by the client.</param>
    /// <param name="grant">The build grant associated with this connection.</param>
    /// <param name="pipeStream">The named pipe stream connected to the client.</param>
    public ClientConnection(Guid connectionId, int processId, ImmutableArray<string> capabilities, BuildGrant grant, NamedPipeServerStream pipeStream)
    {
        ConnectionId = connectionId;
        ProcessId = processId;
        Capabilities = capabilities;
        Grant = grant;
        PipeStream = pipeStream;
        Reader = new BinaryReader(pipeStream, System.Text.Encoding.UTF8, leaveOpen: true);
        Writer = new BinaryWriter(pipeStream, System.Text.Encoding.UTF8, leaveOpen: true);
    }

    public void Dispose()
    {
        try
        {
            Writer.Dispose();
        }
        catch (IOException)
        {
            // The pipe may already be broken if the client disconnected.
        }

        Reader.Dispose();
        PipeStream.Dispose();
    }
}
