// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.IO.Pipes;

namespace Microsoft.Build.Coordinator;

/// <summary>
///  Tracks the pipe connection for a single MSBuild client alongside its <see cref="BuildGrant"/>.
/// </summary>
internal sealed class ClientConnection : IDisposable
{
    /// <summary>
    ///  The build grant associated with this connection.
    /// </summary>
    public BuildGrant Grant { get; }

    /// <summary>
    ///  The named pipe stream connected to the client.
    /// </summary>
    public NamedPipeServerStream PipeStream { get; }

    /// <summary>
    ///  A reader for deserializing client messages from the pipe.
    /// </summary>
    public BinaryReader Reader { get; }

    /// <summary>
    ///  A writer for serializing server messages to the pipe.
    /// </summary>
    public BinaryWriter Writer { get; }

    /// <summary>
    ///  Creates a new client connection wrapping the given grant and pipe stream.
    /// </summary>
    /// <param name="grant">The build grant associated with this connection.</param>
    /// <param name="pipeStream">The named pipe stream connected to the client.</param>
    public ClientConnection(BuildGrant grant, NamedPipeServerStream pipeStream)
    {
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
