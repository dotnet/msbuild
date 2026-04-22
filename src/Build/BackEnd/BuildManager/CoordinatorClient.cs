// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd;

/// <summary>
///  Client for communicating with the MSBuild build coordinator.
///  Handles connecting to (or launching) the coordinator, requesting a node grant,
///  sending heartbeats, and releasing the grant.
/// </summary>
internal sealed class CoordinatorClient : IDisposable
{
    private readonly NamedPipeClientStream _pipeStream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly Timer _heartbeatTimer;
    private bool _disposed;

    /// <summary>
    ///  The number of nodes granted by the coordinator.
    /// </summary>
    public int GrantedNodes { get; }

    private CoordinatorClient(NamedPipeClientStream pipeStream, BinaryReader reader, BinaryWriter writer, int grantedNodes, int heartbeatIntervalMs)
    {
        _pipeStream = pipeStream;
        _reader = reader;
        _writer = writer;
        GrantedNodes = grantedNodes;

        _heartbeatTimer = new Timer(
            SendHeartbeat,
            state: null,
            dueTime: heartbeatIntervalMs,
            period: heartbeatIntervalMs);
    }

    /// <summary>
    ///  Attempts to connect to the coordinator and request a node grant.
    ///  Returns null if the coordinator is not available or an error occurs.
    /// </summary>
    public static CoordinatorClient? TryConnect(int requestedNodes, int connectionTimeoutMs = 5000)
    {
        string pipeName = Protocol.GetPipeName();

        NamedPipeClientStream? pipeStream = null;

        try
        {
            pipeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            // Try to connect to an existing coordinator.
            if (!TryConnectToPipe(pipeStream, connectionTimeoutMs))
            {
                // No coordinator running. Try to launch one and retry.
                if (!TryLaunchCoordinator())
                {
                    pipeStream.Dispose();
                    return null;
                }

                // The first pipe may be in a bad state after a failed connect. Create a new one.
                pipeStream.Dispose();
                pipeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                if (!TryConnectToPipe(pipeStream, connectionTimeoutMs))
                {
                    pipeStream.Dispose();
                    return null;
                }
            }

            return TryNegotiate(pipeStream, requestedNodes, EnvironmentUtilities.CurrentProcessId);
        }
        catch (Exception) when (!Debugger.IsAttached)
        {
            // Any failure in coordinator communication should not break the build.
            pipeStream?.Dispose();
            return null;
        }
    }

    /// <summary>
    ///  Attempts to connect to a coordinator at the given pipe name and request a node grant.
    ///  This overload does not attempt to launch the coordinator and is intended for testing.
    /// </summary>
    internal static CoordinatorClient? TryConnectToServer(string pipeName, int requestedNodes, int processId, int connectionTimeoutMs = 5000)
    {
        NamedPipeClientStream? pipeStream = null;

        try
        {
            pipeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            if (!TryConnectToPipe(pipeStream, connectionTimeoutMs))
            {
                pipeStream.Dispose();
                return null;
            }

            return TryNegotiate(pipeStream, requestedNodes, processId);
        }
        catch (Exception) when (!Debugger.IsAttached)
        {
            pipeStream?.Dispose();
            return null;
        }
    }

    /// <summary>
    ///  Performs the request/response negotiation over an already-connected pipe.
    ///  On failure, disposes the pipe and returns null.
    /// </summary>
    private static CoordinatorClient? TryNegotiate(NamedPipeClientStream pipeStream, int requestedNodes, int processId)
    {
        int heartbeatIntervalMs = GetEnvironmentInt(
            Protocol.HeartbeatIntervalEnvironmentVariable,
            Protocol.DefaultHeartbeatIntervalMs);

        BinaryReader reader = new(pipeStream, System.Text.Encoding.UTF8, leaveOpen: true);
        BinaryWriter writer = new(pipeStream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Send the node request.
        new RequestNodesMessage(requestedNodes, processId).Write(writer);

        // Read the response.
        ServerMessage response = ServerMessage.Read(reader);

        switch (response)
        {
            case NodeGrantMessage grant:
                return new CoordinatorClient(pipeStream, reader, writer, grant.GrantedNodes, heartbeatIntervalMs);

            case WaitMessage:
                // Wait for a grant to arrive.
                ServerMessage grantAfterWait = ServerMessage.Read(reader);

                if (grantAfterWait is NodeGrantMessage deferredGrant)
                {
                    return new CoordinatorClient(pipeStream, reader, writer, deferredGrant.GrantedNodes, heartbeatIntervalMs);
                }

                // Unexpected response after wait.
                reader.Dispose();
                writer.Dispose();
                pipeStream.Dispose();
                return null;

            default:
                // Error or unexpected response.
                reader.Dispose();
                writer.Dispose();
                pipeStream.Dispose();
                return null;
        }
    }

    /// <summary>
    ///  Releases the node grant and disconnects from the coordinator.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _heartbeatTimer.Dispose();

        try
        {
            ReleaseNodesMessage.Instance.Write(_writer);
        }
        catch (IOException)
        {
            // Pipe may already be broken.
        }

        try
        {
            _writer.Dispose();
        }
        catch (IOException)
        {
            // Flush in BinaryWriter.Dispose can throw on broken pipe.
        }

        _reader.Dispose();
        _pipeStream.Dispose();
    }

    private void SendHeartbeat(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            HeartbeatMessage.Instance.Write(_writer);
        }
        catch (IOException)
        {
            // Pipe broken — nothing we can do. The build continues
            // with whatever nodes were already granted.
        }
    }

    private static bool TryConnectToPipe(NamedPipeClientStream pipeStream, int timeoutMs)
    {
        try
        {
            pipeStream.Connect(timeoutMs);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static bool TryLaunchCoordinator()
    {
        try
        {
            ProcessStartInfo? startInfo = TryGetStartInfo();

            if (startInfo is null)
            {
                return false;
            }

            Process? process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception) when (!Debugger.IsAttached)
        {
            return false;
        }
    }

    private static ProcessStartInfo? TryGetStartInfo()
    {
        string msbuildDir = BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory;

        // Try the .dll form first (dotnet exec), then .exe.
        string coordinatorDll = Path.Combine(msbuildDir, "MSBuild.Coordinator.dll");
        string coordinatorExe = Path.Combine(msbuildDir, "MSBuild.Coordinator.exe");

        if (File.Exists(coordinatorDll) &&
            CurrentHost.GetCurrentHost() is string dotnetHost)
        {
            return new ProcessStartInfo
            {
                FileName = dotnetHost,
                Arguments = $"\"{coordinatorDll}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        // Full Framework — fall back to the native .exe if available.
        if (File.Exists(coordinatorExe))
        {
            return new ProcessStartInfo
            {
                FileName = coordinatorExe,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        return null;
    }

    private static int GetEnvironmentInt(string variable, int defaultValue)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        return int.TryParse(value, out int result) ? result : defaultValue;
    }
}
