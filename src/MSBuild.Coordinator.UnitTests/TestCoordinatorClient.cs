// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;
using Shouldly;

namespace Microsoft.Build.Coordinator.UnitTests;

/// <summary>
///  A test client that owns a coordinator pipe connection and its protocol reader and writer.
/// </summary>
/// <remarks>
///  Connection establishment is asynchronous. Protocol messages use the same synchronous
///  reader and writer behavior as the coordinator server tests used before this helper was extracted.
/// </remarks>
internal sealed class TestCoordinatorClient : IDisposable
{
    private readonly NamedPipeClientStream _pipeStream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;

    /// <summary>
    ///  Creates a client that owns the connected pipe stream.
    /// </summary>
    /// <param name="pipeStream">The connected coordinator pipe stream.</param>
    private TestCoordinatorClient(NamedPipeClientStream pipeStream)
    {
        _pipeStream = pipeStream;
        _reader = new BinaryReader(pipeStream, Encoding.UTF8, leaveOpen: true);
        _writer = new BinaryWriter(pipeStream, Encoding.UTF8, leaveOpen: true);
    }

    /// <summary>
    ///  Disposes the protocol reader, writer, and owned pipe connection.
    /// </summary>
    public void Dispose()
    {
        _writer.Dispose();
        _reader.Dispose();
        _pipeStream.Dispose();
    }

    /// <summary>
    ///  Connects to a coordinator pipe and optionally performs the client handshake.
    /// </summary>
    /// <param name="pipeName">The platform-specific coordinator pipe name.</param>
    /// <param name="processId">The process ID sent in the handshake, or the current process ID when omitted.</param>
    /// <param name="supportsNestedGrants">Whether the handshake advertises nested-grant support.</param>
    /// <param name="sendHandshake">Whether to perform the handshake before returning the client.</param>
    /// <returns>
    ///  The connected test client.
    /// </returns>
    public static async Task<TestCoordinatorClient> ConnectAsync(
        string pipeName,
        int? processId = null,
        bool supportsNestedGrants = false,
        bool sendHandshake = true)
    {
        NamedPipeClientStream pipeStream = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        TestCoordinatorClient? client = null;

        try
        {
            await pipeStream.ConnectAsync(5000);
            client = new TestCoordinatorClient(pipeStream);

            // Protocol validation tests can opt out and send an invalid first message.
            if (sendHandshake)
            {
                client.Handshake(processId, supportsNestedGrants);
            }

            return client;
        }
        catch
        {
            // Ownership transfers from the local pipe to the client after construction.
            if (client is null)
            {
                pipeStream.Dispose();
            }
            else
            {
                client.Dispose();
            }

            throw;
        }
    }

    /// <summary>
    ///  Sends a client handshake and verifies that the server accepts it.
    /// </summary>
    /// <param name="processId">The process ID sent in the handshake, or the current process ID when omitted.</param>
    /// <param name="supportsNestedGrants">Whether the handshake advertises nested-grant support.</param>
    public void Handshake(int? processId = null, bool supportsNestedGrants = false)
    {
        Send(new ClientHandshakeMessage(
            Guid.NewGuid(),
            processId is int pid ? pid : EnvironmentUtilities.CurrentProcessId,
            supportsNestedGrants ? [Capabilities.NestedGrants] : []));
        ReadServerMessage().ShouldBeOfType<ServerHandshakeMessage>();
    }

    /// <summary>
    ///  Requests to join an existing root grant and reads the server response.
    /// </summary>
    /// <param name="grantId">The root grant identifier.</param>
    /// <param name="requestedNodes">The requested node count.</param>
    /// <returns>
    ///  The server response.
    /// </returns>
    public ServerMessage JoinGrant(Guid grantId, int requestedNodes)
        => SendAndReceive(new JoinGrantMessage(grantId, requestedNodes));

    /// <summary>
    ///  Reads the next message sent by the coordinator server.
    /// </summary>
    /// <returns>
    ///  The next server message.
    /// </returns>
    public ServerMessage ReadServerMessage()
        => _reader.ReadServerMessage();

    /// <summary>
    ///  Releases the client's current node grant.
    /// </summary>
    public void ReleaseNodes()
        => Send(ReleaseNodesMessage.Instance);

    /// <summary>
    ///  Requests nodes and reads the immediate server response.
    /// </summary>
    /// <param name="requestedNodes">The requested node count.</param>
    /// <returns>
    ///  The immediate grant, wait, or error response.
    /// </returns>
    public ServerMessage RequestNodes(int requestedNodes)
    {
        SendRequestNodes(requestedNodes);
        return ReadServerMessage();
    }

    /// <summary>
    ///  Sends a client protocol message without reading a response.
    /// </summary>
    /// <param name="message">The message to send.</param>
    public void Send(ClientMessage message)
        => _writer.Write(message);

    /// <summary>
    ///  Sends a heartbeat message.
    /// </summary>
    public void SendHeartbeat()
        => Send(HeartbeatMessage.Instance);

    /// <summary>
    ///  Sends a node request without reading the server response.
    /// </summary>
    /// <param name="requestedNodes">The requested node count.</param>
    public void SendRequestNodes(int requestedNodes)
        => Send(new RequestNodesMessage(requestedNodes));

    /// <summary>
    ///  Sends a client message and reads the immediate server response.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <returns>
    ///  The immediate server response.
    /// </returns>
    private ServerMessage SendAndReceive(ClientMessage message)
    {
        Send(message);
        return ReadServerMessage();
    }
}
