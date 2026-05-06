// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
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
    private readonly ICoordinatorLogger _logger;
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
        ICoordinatorLogger logger)
    {
        _pipeStream = pipeStream;
        _reader = reader;
        _writer = writer;
        _logger = logger;
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
    public static CoordinatorClient? TryConnect(int requestedNodes, CoordinatorSettings? settings = null)
    {
        settings ??= CoordinatorSettings.FromEnvironment();
        ICoordinatorLogger logger = DefaultLogger.Instance;

        NamedPipeClientStream? pipeStream = null;

        try
        {
            pipeStream = new NamedPipeClientStream(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            logger.WriteLine($"CoordinatorClient: Connecting to pipe '{settings.PipeName}'");

            // Try to connect to an existing coordinator.
            if (!TryConnectToPipe(pipeStream, settings.ConnectionTimeoutMs))
            {
                logger.WriteLine("CoordinatorClient: No coordinator running, attempting to launch");

                // No coordinator running. Try to launch one and retry.
                if (!TryLaunchCoordinator(logger))
                {
                    logger.WriteLine("CoordinatorClient: Failed to launch coordinator");
                    pipeStream.Dispose();
                    return null;
                }

                // The first pipe may be in a bad state after a failed connect. Create a new one.
                pipeStream.Dispose();
                pipeStream = new NamedPipeClientStream(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                logger.WriteLine("CoordinatorClient: Retrying connection after launch");

                if (!TryConnectToPipe(pipeStream, settings.ConnectionTimeoutMs))
                {
                    logger.WriteLine("CoordinatorClient: Retry connection failed");
                    pipeStream.Dispose();
                    return null;
                }
            }

            logger.WriteLine("CoordinatorClient: Connected to coordinator");

            return TryNegotiate(pipeStream, requestedNodes, settings, logger);
        }
        catch (Exception ex) when (!Debugger.IsAttached)
        {
            logger.WriteLine($"CoordinatorClient: Exception during connect: {ex.Message}");

            // Any failure in coordinator communication should not break the build.
            pipeStream?.Dispose();
            return null;
        }
    }

    /// <summary>
    ///  Attempts to connect to a coordinator using the provided settings and request a node grant.
    ///  This overload does not attempt to launch the coordinator and is intended for testing.
    /// </summary>
    internal static CoordinatorClient? TryConnectToServer(
        int requestedNodes,
        CoordinatorSettings? settings = null,
        ICoordinatorLogger? logger = null)
    {
        settings ??= CoordinatorSettings.Default;
        logger ??= DefaultLogger.Instance;
        NamedPipeClientStream? pipeStream = null;

        try
        {
            pipeStream = new NamedPipeClientStream(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            logger.WriteLine($"CoordinatorClient: Connecting to test pipe '{settings.PipeName}'");

            if (!TryConnectToPipe(pipeStream, settings.ConnectionTimeoutMs))
            {
                logger.WriteLine("CoordinatorClient: Test connection timed out");
                pipeStream.Dispose();
                return null;
            }

            logger.WriteLine("CoordinatorClient: Connected to test server");

            return TryNegotiate(pipeStream, requestedNodes, settings, logger);
        }
        catch (Exception ex) when (!Debugger.IsAttached)
        {
            logger.WriteLine($"CoordinatorClient: Exception during test connect: {ex.Message}");
            pipeStream?.Dispose();
            return null;
        }
    }

    /// <summary>
    ///  Performs the request/response negotiation over an already-connected pipe.
    ///  On failure, disposes the pipe and returns null.
    /// </summary>
    private static CoordinatorClient? TryNegotiate(
        NamedPipeClientStream pipeStream,
        int requestedNodes,
        CoordinatorSettings settings,
        ICoordinatorLogger logger)
    {
        var reader = new BinaryReader(pipeStream, System.Text.Encoding.UTF8, leaveOpen: true);
        var writer = new BinaryWriter(pipeStream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Send the node request.
        logger.WriteLine($"CoordinatorClient: Requesting {requestedNodes} nodes (PID {settings.ProcessId})");
        writer.Write(new RequestNodesMessage(requestedNodes, settings.ProcessId));

        // Read the response.
        ServerMessage response = reader.ReadServerMessage();

        switch (response)
        {
            case NodeGrantMessage grant:
                logger.WriteLine($"CoordinatorClient: Granted {grant.GrantedNodes} nodes");
                return new CoordinatorClient(pipeStream, reader, writer, grant.GrantedNodes, settings.HeartbeatIntervalMs, logger);

            case WaitMessage:
                logger.WriteLine("CoordinatorClient: Received WaitMessage, waiting for deferred grant");

                // Wait for a grant to arrive.
                ServerMessage grantAfterWait = reader.ReadServerMessage();

                if (grantAfterWait is NodeGrantMessage deferredGrant)
                {
                    logger.WriteLine($"CoordinatorClient: Deferred grant received: {deferredGrant.GrantedNodes} nodes");
                    return new CoordinatorClient(pipeStream, reader, writer, deferredGrant.GrantedNodes, settings.HeartbeatIntervalMs, logger);
                }

                logger.WriteLine($"CoordinatorClient: Unexpected response after wait: {grantAfterWait.GetType().Name}");

                // Unexpected response after wait.
                reader.Dispose();
                writer.Dispose();
                pipeStream.Dispose();
                return null;

            default:
                logger.WriteLine($"CoordinatorClient: Unexpected response: {response.GetType().Name}");

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

        // Stop heartbeat timer and wait for any in-flight callback to complete.
        // This prevents SendHeartbeat from running after resources are disposed.
        using (var disposeEvent = new ManualResetEvent(false))
        {
            _heartbeatTimer.Dispose(disposeEvent);
            disposeEvent.WaitOne();
        }

        _logger.WriteLine($"CoordinatorClient: Releasing grant ({GrantedNodes} nodes)");

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
            _logger.WriteLine("CoordinatorClient: Heartbeat failed (pipe broken)");

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

    private static bool TryLaunchCoordinator(ICoordinatorLogger logger)
    {
        try
        {
            ProcessStartInfo? startInfo = TryGetStartInfo();

            if (startInfo is null)
            {
                return false;
            }

            logger.WriteLine($"CoordinatorClient: Launching coordinator: {startInfo.FileName} {startInfo.Arguments}");

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
}
