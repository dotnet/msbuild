// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
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
    private readonly ICoordinatorDebugOutput _output;
    private volatile bool _disposed;

    /// <summary>
    ///  The unique identifier for this connection to the coordinator.
    /// </summary>
    public Guid ConnectionId { get; }

    /// <summary>
    ///  The capabilities advertised by the server during handshake.
    /// </summary>
    public ImmutableArray<string> ServerCapabilities { get; }

    /// <summary>
    ///  The number of nodes granted by the coordinator.
    /// </summary>
    public int GrantedNodes { get; }

    /// <summary>
    ///  The time spent waiting for a deferred node grant, or <see langword="null"/> if no wait occurred.
    /// </summary>
    public TimeSpan? WaitDuration { get; private init; }

    private CoordinatorClient(
        Guid connectionId,
        ImmutableArray<string> serverCapabilities,
        NamedPipeClientStream pipeStream,
        BinaryReader reader,
        BinaryWriter writer,
        int grantedNodes,
        int heartbeatIntervalMs,
        ICoordinatorDebugOutput output)
    {
        ConnectionId = connectionId;
        ServerCapabilities = serverCapabilities;
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
        ICoordinatorDebugOutput output = DefaultDebugOutput.Instance;

        NamedPipeClientStream? pipeStream = null;

        try
        {
            pipeStream = new NamedPipeClientStream(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            output.WriteLine($"CoordinatorClient: Connecting to pipe '{settings.PipeName}'");

            // Fast probe: use a short timeout to check if a coordinator is already running.
            // If no pipe exists, this fails quickly (~200ms) instead of waiting the full 5s.
            if (!TryConnectToPipe(pipeStream, settings.InitialConnectionTimeoutMs))
            {
                output.WriteLine("CoordinatorClient: No coordinator running, attempting to launch");

                pipeStream.Dispose();
#pragma warning disable CA2000 // Ownership transferred to TryNegotiate or disposed in outer finally
                pipeStream = TryLaunchAndConnect(settings, loggingService, output);
#pragma warning restore CA2000

                if (pipeStream is null)
                {
                    return null;
                }
            }

            output.WriteLine("CoordinatorClient: Connected to coordinator");

            CoordinatorClient? client = TryNegotiate(pipeStream, requestedNodes, settings, output, loggingService);
            pipeStream = null; // Ownership transferred unconditionally; TryNegotiate disposes on failure.

            if (client is null)
            {
                loggingService.LogComment(BuildEventContext.Invalid, MessageImportance.Normal, "CoordinatorFailedToNegotiate");
            }

            return client;
        }
        catch (Exception ex) when (!Debugger.IsAttached)
        {
            output.WriteLine($"CoordinatorClient: Exception during connect: {ex.Message}");
            loggingService.LogComment(BuildEventContext.Invalid, MessageImportance.Normal, "CoordinatorFailedToConnect");

            // Any failure in coordinator communication should not break the build.
            pipeStream?.Dispose();
            return null;
        }
    }

    /// <summary>
    ///  Acquires a named mutex to serialize coordinator launches, then either launches
    ///  the coordinator or detects one already running via the server mutex.
    ///  Releases the launch mutex before the pipe readiness wait so concurrent clients
    ///  can wait for pipe readiness in parallel.
    /// </summary>
    /// <param name="settings">Coordinator connection settings.</param>
    /// <param name="loggingService">The MSBuild logging service for user-visible messages.</param>
    /// <param name="output">Debug trace output.</param>
    /// <returns>
    ///  A connected pipe stream, or <see langword="null"/> if the coordinator could not be started or reached.
    /// </returns>
    private static NamedPipeClientStream? TryLaunchAndConnect(
        CoordinatorSettings settings,
        ILoggingService loggingService,
        ICoordinatorDebugOutput output)
    {
        // Acquire a launch mutex so only one client launches the coordinator.
        // Other clients racing here will block until the launcher finishes, then
        // detect the running coordinator via its server mutex.
        using Mutex launchMutex = new(initiallyOwned: false, settings.LaunchMutexName);

        try
        {
            if (!launchMutex.WaitOne(settings.ConnectionTimeoutMs))
            {
                output.WriteLine("CoordinatorClient: Timed out waiting for launch mutex");
                loggingService.LogComment(BuildEventContext.Invalid, MessageImportance.Normal, "CoordinatorLaunchTimedOut");
                return null;
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous holder crashed — we now own the mutex and should proceed with launch.
            output.WriteLine("CoordinatorClient: Acquired abandoned launch mutex");
        }

        try
        {
            // Check the server mutex to determine if a coordinator is already running.
            // This is much faster than attempting a pipe connect with a full timeout.
            if (IsCoordinatorRunning(settings, output))
            {
                output.WriteLine("CoordinatorClient: Coordinator already running (server mutex exists)");
            }
            else
            {
                // No coordinator running. Launch one.
                if (!TryLaunchCoordinator(loggingService, output))
                {
                    output.WriteLine("CoordinatorClient: Failed to launch coordinator");
                    return null;
                }

                // Wait for the coordinator to advertise its server mutex, indicating
                // it has started and is about to open the pipe.
                if (!WaitForCoordinatorStartup(settings, output))
                {
                    output.WriteLine("CoordinatorClient: Coordinator did not start in time");
                    loggingService.LogComment(BuildEventContext.Invalid, MessageImportance.Normal, "CoordinatorFailedToConnect");
                    return null;
                }
            }
        }
        finally
        {
            // Release the launch mutex before the pipe readiness wait so concurrent
            // clients can check the server mutex and wait for the pipe in parallel.
            launchMutex.ReleaseMutex();
        }

        // Wait for the coordinator to be ready by connecting to its pipe.
        NamedPipeClientStream? pipeStream = new(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            if (!TryConnectToPipe(pipeStream, settings.ConnectionTimeoutMs))
            {
                output.WriteLine("CoordinatorClient: Failed to connect to coordinator");
                loggingService.LogComment(BuildEventContext.Invalid, MessageImportance.Normal, "CoordinatorFailedToConnect");
                return null;
            }

            output.WriteLine("CoordinatorClient: Coordinator launched successfully");

            NamedPipeClientStream connected = pipeStream;
            pipeStream = null; // Ownership transferred to caller.

            return connected;
        }
        finally
        {
            pipeStream?.Dispose();
        }
    }

    /// <summary>
    ///  Checks whether a coordinator process is running by probing its server mutex.
    /// </summary>
    private static bool IsCoordinatorRunning(CoordinatorSettings settings, ICoordinatorDebugOutput output)
    {
        try
        {
            if (Mutex.TryOpenExisting(settings.ServerMutexName, out Mutex? existingMutex))
            {
                existingMutex.Dispose();
                return true;
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"CoordinatorClient: Failed to check server mutex: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    ///  Polls for the coordinator's server mutex to appear, indicating the process has started.
    /// </summary>
    private static bool WaitForCoordinatorStartup(CoordinatorSettings settings, ICoordinatorDebugOutput output)
    {
        Stopwatch sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < settings.ConnectionTimeoutMs)
        {
            if (IsCoordinatorRunning(settings, output))
            {
                output.WriteLine($"CoordinatorClient: Server mutex appeared after {sw.ElapsedMilliseconds}ms");
                return true;
            }

            Thread.Sleep(50);
        }

        return false;
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
        ICoordinatorDebugOutput output,
        ILoggingService? loggingService)
    {
        var reader = new BinaryReader(pipeStream, Encoding.UTF8, leaveOpen: true);
        var writer = new BinaryWriter(pipeStream, Encoding.UTF8, leaveOpen: true);
        var pipe = pipeStream;

        try
        {
            // Perform the handshake.
            var connectionId = Guid.NewGuid();
            if (TrySendHandshake(connectionId, settings.ProcessId, reader, writer, output) is not ServerHandshakeMessage serverHandshake)
            {
                return null;
            }

            // Send the node request.
            output.WriteLine($"CoordinatorClient: Requesting {requestedNodes} nodes (PID {settings.ProcessId}, ConnectionId {connectionId})");
            writer.Write(new RequestNodesMessage(requestedNodes));

            // Read the response.
            ServerMessage response = reader.ReadServerMessage();

            switch (response)
            {
                case NodeGrantMessage grant:
                    output.WriteLine($"CoordinatorClient: Granted {grant.GrantedNodes} nodes");
                    loggingService?.LogComment(BuildEventContext.Invalid, MessageImportance.Normal, "CoordinatorNodeGrantReceived", grant.GrantedNodes);

                    var client = new CoordinatorClient(connectionId, serverHandshake.Capabilities, pipeStream, reader, writer, grant.GrantedNodes, settings.HeartbeatIntervalMs, output);

                    // Ownership transferred to client
                    reader = null;
                    writer = null;
                    pipe = null;

                    return client;

                case WaitMessage:
                    output.WriteLine("CoordinatorClient: Received WaitMessage, waiting for deferred grant");
                    loggingService?.LogComment(BuildEventContext.Invalid, MessageImportance.High, "CoordinatorWaitingForNodes");

                    var waitTimer = Stopwatch.StartNew();

                    // Send heartbeats while waiting so the server doesn't consider us stale.
                    using (Timer heartbeatPump = CreateHeartbeatPump(writer, settings.HeartbeatIntervalMs))
                    {
                        ServerMessage grantAfterWait = reader.ReadServerMessage();

                        if (grantAfterWait is NodeGrantMessage deferredGrant)
                        {
                            waitTimer.Stop();

                            output.WriteLine($"CoordinatorClient: Deferred grant received: {deferredGrant.GrantedNodes} nodes (waited {waitTimer.Elapsed.TotalSeconds:F2}s)");
                            loggingService?.LogComment(BuildEventContext.Invalid, MessageImportance.Normal, "CoordinatorNodeGrantReceived", deferredGrant.GrantedNodes);

                            var deferredClient = new CoordinatorClient(connectionId, serverHandshake.Capabilities, pipeStream, reader, writer, deferredGrant.GrantedNodes, settings.HeartbeatIntervalMs, output)
                            {
                                WaitDuration = waitTimer.Elapsed,
                            };

                            // Ownership transferred to deferred client
                            reader = null;
                            writer = null;
                            pipe = null;

                            return deferredClient;
                        }

                        if (grantAfterWait is ErrorMessage waitError)
                        {
                            output.WriteLine($"CoordinatorClient: Server error while waiting: {waitError.Message} (waited {waitTimer.Elapsed.TotalSeconds:F1}s)");
                        }
                        else
                        {
                            output.WriteLine($"CoordinatorClient: Unexpected response after wait: {grantAfterWait.GetType().Name} (waited {waitTimer.Elapsed.TotalSeconds:F1}s)");
                        }
                    }

                    return null;

                case ErrorMessage requestError:
                    output.WriteLine($"CoordinatorClient: Server error: {requestError.Message}");
                    return null;

                default:
                    output.WriteLine($"CoordinatorClient: Unexpected response: {response.GetType().Name}");
                    return null;
            }
        }
        finally
        {
            // On success, all three are set to null to indicate ownership was
            // transferred to the CoordinatorClient instance. On failure, dispose
            // whatever remains.
            reader?.Dispose();
            writer?.Dispose();
            pipe?.Dispose();
        }
    }

    /// <summary>
    ///  Sends the client handshake and reads the server's handshake response.
    /// </summary>
    /// <param name="connectionId">The unique connection identifier to send.</param>
    /// <param name="processId">The current process ID to send.</param>
    /// <param name="reader">The binary reader for the coordinator pipe.</param>
    /// <param name="writer">The binary writer for the coordinator pipe.</param>
    /// <param name="output">Debug trace output.</param>
    /// <returns>
    ///  The server's handshake message, or <see langword="null"/> if the handshake was rejected.
    /// </returns>
    private static ServerHandshakeMessage? TrySendHandshake(
        Guid connectionId,
        int processId,
        BinaryReader reader,
        BinaryWriter writer,
        ICoordinatorDebugOutput output)
    {
        output.WriteLine($"CoordinatorClient: Sending handshake (ConnectionId {connectionId})");
        writer.Write(new ClientHandshakeMessage(connectionId, processId, []));

        ServerMessage response = reader.ReadServerMessage();

        if (response is ErrorMessage error)
        {
            output.WriteLine($"CoordinatorClient: Server rejected handshake: {error.Message}");
            return null;
        }

        if (response is not ServerHandshakeMessage serverHandshake)
        {
            output.WriteLine($"CoordinatorClient: Unexpected handshake response: {response.GetType().Name}");
            return null;
        }

        output.WriteLine($"CoordinatorClient: Handshake complete (server capabilities: [{string.Join(", ", serverHandshake.Capabilities)}])");

        return serverHandshake;
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

    private static bool TryLaunchCoordinator(ILoggingService loggingService, ICoordinatorDebugOutput output)
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
