// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd;

/// <summary>
///  Client for communicating with the MSBuild build coordinator.
///  Handles connecting to (or launching) the coordinator, requesting a node grant,
///  sending heartbeats, and releasing the grant.
/// </summary>
internal sealed partial class CoordinatorClient : IDisposable
{
    private readonly NamedPipeClientStream _pipeStream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly Timer _heartbeatTimer;
    private readonly ICoordinatorOutput _output;
    private volatile bool _disposed;

    /// <summary>
    ///  The number of nodes granted by the coordinator.
    /// </summary>
    public int GrantedNodes { get; }

    private CoordinatorClient(
        NamedPipeClientStream pipeStream,
        BinaryReader reader,
        BinaryWriter writer,
        int grantedNodes,
        int heartbeatIntervalMs,
        ICoordinatorOutput output)
    {
        _pipeStream = pipeStream;
        _reader = reader;
        _writer = writer;
        _output = output;
        GrantedNodes = grantedNodes;

        _heartbeatTimer = new Timer(
            SendHeartbeat,
            state: null,
            dueTime: heartbeatIntervalMs,
            period: heartbeatIntervalMs);
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

        // Stop heartbeat timer and wait for any in-flight callback to complete.
        // This prevents SendHeartbeat from running after resources are disposed.
        using (var disposeEvent = new ManualResetEvent(false))
        {
            _heartbeatTimer.Dispose(disposeEvent);
            disposeEvent.WaitOne();
        }

        _output.WriteLine($"CoordinatorClient: Releasing grant ({GrantedNodes} nodes)");

        try
        {
            _writer.Write(ReleaseNodesMessage.Instance);
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

    /// <summary>
    ///  Attempts to connect to the coordinator and request a node grant.
    ///  Returns null if the coordinator is not available or an error occurs.
    /// </summary>
    /// <param name="requestedNodes">The maximum number of nodes to request from the coordinator.</param>
    /// <param name="settings">Coordinator connection settings (pipe name, timeouts, etc.).</param>
    /// <param name="loggingService">The MSBuild logging service used to emit user-visible messages.</param>
    /// <returns>
    ///  A connected <see cref="CoordinatorClient"/> instance, or <see langword="null"/> if the coordinator is not available.
    /// </returns>
    public static CoordinatorClient? TryConnect(int requestedNodes, CoordinatorSettings settings, ILoggingService loggingService)
    {
        ICoordinatorOutput output = DefaultOutput.Instance;

        NamedPipeClientStream? pipeStream = null;

        try
        {
            pipeStream = new NamedPipeClientStream(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            output.WriteLine($"CoordinatorClient: Connecting to pipe '{settings.PipeName}'");

            // Try to connect to an existing coordinator.
            if (!TryConnectToPipe(pipeStream, settings.ConnectionTimeoutMs))
            {
                output.WriteLine("CoordinatorClient: No coordinator running, attempting to launch");

                // No coordinator running. Try to launch one and retry.
                if (!TryLaunchCoordinator(loggingService, output))
                {
                    output.WriteLine("CoordinatorClient: Failed to launch coordinator");
                    pipeStream.Dispose();
                    return null;
                }

                // The first pipe may be in a bad state after a failed connect. Create a new one.
                pipeStream.Dispose();
                pipeStream = new NamedPipeClientStream(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                output.WriteLine("CoordinatorClient: Retrying connection after launch");

                if (!TryConnectToPipe(pipeStream, settings.ConnectionTimeoutMs))
                {
                    output.WriteLine("CoordinatorClient: Retry connection failed");
                    pipeStream.Dispose();
                    return null;
                }
            }

            output.WriteLine("CoordinatorClient: Connected to coordinator");

            return TryNegotiate(pipeStream, requestedNodes, settings, output, loggingService);
        }
        catch (Exception ex) when (!Debugger.IsAttached)
        {
            output.WriteLine($"CoordinatorClient: Exception during connect: {ex.Message}");

            // Any failure in coordinator communication should not break the build.
            pipeStream?.Dispose();
            return null;
        }
    }

    /// <summary>
    ///  Performs the request/response negotiation over an already-connected pipe.
    ///  On failure, disposes the pipe and returns null.
    /// </summary>
    /// <param name="pipeStream">The connected named pipe stream.</param>
    /// <param name="requestedNodes">The number of nodes to request.</param>
    /// <param name="settings">Coordinator settings including heartbeat interval and process ID.</param>
    /// <param name="output">Debug trace output for diagnostic logging.</param>
    /// <param name="loggingService">Optional MSBuild logging service for user-visible messages.</param>
    /// <returns>
    ///  A connected <see cref="CoordinatorClient"/> instance, or <see langword="null"/> if negotiation fails.
    /// </returns>
    private static CoordinatorClient? TryNegotiate(
        NamedPipeClientStream pipeStream,
        int requestedNodes,
        CoordinatorSettings settings,
        ICoordinatorOutput output,
        ILoggingService? loggingService)
    {
        var reader = new BinaryReader(pipeStream, Encoding.UTF8, leaveOpen: true);
        var writer = new BinaryWriter(pipeStream, Encoding.UTF8, leaveOpen: true);

        try
        {
            // Send the node request.
            output.WriteLine($"CoordinatorClient: Requesting {requestedNodes} nodes (PID {settings.ProcessId})");
            writer.Write(new RequestNodesMessage(requestedNodes, settings.ProcessId));

            // Read the response.
            ServerMessage response = reader.ReadServerMessage();

            switch (response)
            {
                case NodeGrantMessage grant:
                    output.WriteLine($"CoordinatorClient: Granted {grant.GrantedNodes} nodes");
                    loggingService?.LogComment(BuildEventContext.Invalid, MessageImportance.Normal, "CoordinatorNodeGrantReceived", grant.GrantedNodes);

                    var client = new CoordinatorClient(pipeStream, reader, writer, grant.GrantedNodes, settings.HeartbeatIntervalMs, output);

                    // Ownership transferred to client
                    reader = null;
                    writer = null;

                    return client;

                case WaitMessage:
                    output.WriteLine("CoordinatorClient: Received WaitMessage, waiting for deferred grant");
                    loggingService?.LogComment(BuildEventContext.Invalid, MessageImportance.High, "CoordinatorWaitingForNodes");

                    // Send heartbeats while waiting so the server doesn't consider us stale.
                    using (Timer heartbeatPump = CreateHeartbeatPump(writer, settings.HeartbeatIntervalMs))
                    {
                        ServerMessage grantAfterWait = reader.ReadServerMessage();

                        if (grantAfterWait is NodeGrantMessage deferredGrant)
                        {
                            output.WriteLine($"CoordinatorClient: Deferred grant received: {deferredGrant.GrantedNodes} nodes");
                            loggingService?.LogComment(BuildEventContext.Invalid, MessageImportance.Normal, "CoordinatorNodeGrantReceived", deferredGrant.GrantedNodes);

                            var deferredClient = new CoordinatorClient(pipeStream, reader, writer, deferredGrant.GrantedNodes, settings.HeartbeatIntervalMs, output);

                            // Ownership transferred to deferred client
                            reader = null;
                            writer = null;

                            return deferredClient;
                        }

                        output.WriteLine($"CoordinatorClient: Unexpected response after wait: {grantAfterWait.GetType().Name}");
                    }

                    return null;

                default:
                    output.WriteLine($"CoordinatorClient: Unexpected response: {response.GetType().Name}");
                    return null;
            }
        }
        finally
        {
            // On success paths, reader/writer are set to null to indicate ownership
            // was transferred to the CoordinatorClient instance, which will dispose them.
            reader?.Dispose();
            writer?.Dispose();

            // If either reader or writer is still non-null, negotiation failed and
            // no CoordinatorClient took ownership of the pipe stream.
            if (reader is not null || writer is not null)
            {
                pipeStream.Dispose();
            }
        }
    }

    /// <summary>
    ///  Creates a heartbeat pump that periodically writes a heartbeat message to the given writer.
    ///  Dispose the returned timer to stop the pump.
    /// </summary>
    /// <param name="writer">The binary writer connected to the coordinator pipe.</param>
    /// <param name="intervalMs">The interval in milliseconds between heartbeats.</param>
    /// <returns>
    ///  A <see cref="Timer"/> that sends heartbeats. Dispose it to stop the pump.
    /// </returns>
    private static Timer CreateHeartbeatPump(BinaryWriter writer, int intervalMs)
        => new(
            static state =>
            {
                try
                {
                    if (state is BinaryWriter w)
                    {
                        w.Write(HeartbeatMessage.Instance);
                    }
                }
                catch
                {
                    // Pipe may be broken; swallow and let the next read detect it.
                }
            },
            state: writer,
            dueTime: intervalMs,
            period: intervalMs);

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

    private static bool TryLaunchCoordinator(ILoggingService loggingService, ICoordinatorOutput output)
    {
        try
        {
            ProcessStartInfo? startInfo = TryGetStartInfo();

            if (startInfo is null)
            {
                return false;
            }

            output.WriteLine($"CoordinatorClient: Launching coordinator: {startInfo.FileName} {startInfo.Arguments}");

            Process? process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception ex) when (!Debugger.IsAttached)
        {
            output.WriteLine($"CoordinatorClient: Exception during launch: {ex}");
            loggingService.LogComment(BuildEventContext.Invalid, MessageImportance.Normal, "CoordinatorFailedToLaunch");
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

    private void SendHeartbeat(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _writer.Write(HeartbeatMessage.Instance);
        }
        catch (IOException)
        {
            _output.WriteLine("CoordinatorClient: Heartbeat failed (pipe broken)");

            // Pipe broken — nothing we can do. The build continues
            // with whatever nodes were already granted.
        }
    }
}
