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
    public BuildGrant Grant { get; }

    public NamedPipeServerStream PipeStream { get; }

    public BinaryReader Reader { get; }

    public BinaryWriter Writer { get; }

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
