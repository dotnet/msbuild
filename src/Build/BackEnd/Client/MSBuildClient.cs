// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Client;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Eventing;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental
{
    /// <summary>
    /// This class is the public entry point for executing builds in msbuild server.
    /// It processes command-line arguments and invokes the build engine.
    /// </summary>
    public sealed class MSBuildClient
    {
        /// <summary>
        /// The build inherits all the environment variables from the client process.
        /// This property allows to add extra environment variables or reset some of the existing ones.
        /// </summary>
        private readonly Dictionary<string, string> _serverEnvironmentVariables;

        /// <summary>
        /// The console mode we had before the build.
        /// </summary>
        private uint? _originalConsoleMode;

        /// <summary>
        /// Full path to current MSBuild.exe if executable is MSBuild.exe,
        /// or to version of MSBuild.dll found to be associated with the current process.
        /// </summary>
        private readonly string _msbuildLocation;

        /// <summary>
        /// The command line to process.
        /// The first argument on the command line is assumed to be the name/path of the executable, and is ignored.
        /// </summary>
        private readonly string[] _commandLine;

        /// <summary>
        /// The MSBuild client execution result.
        /// </summary>
        private readonly MSBuildClientExitResult _exitResult;

        /// <summary>
        /// Whether MSBuild server finished the build.
        /// </summary>
        private bool _buildFinished = false;

        /// <summary>
        /// Handshake between server and client.
        /// </summary>
        private readonly ServerNodeHandshake _handshake;

        /// <summary>
        /// The named pipe name for client-server communication.
        /// </summary>
        private readonly string _pipeName;

        /// <summary>
        /// The named pipe stream for client-server communication.
        /// </summary>
        private NamedPipeClientStream _nodeStream = null!;

        /// <summary>
        /// A way to cache a byte array when writing out packets
        /// </summary>
        private readonly MemoryStream _packetMemoryStream;

        /// <summary>
        /// A binary writer to help write into <see cref="_packetMemoryStream"/>
        /// </summary>
        private readonly BinaryWriter _binaryWriter;

        /// <summary>
        /// Used to estimate the size of the build with an ETW trace.
        /// </summary>
        private int _numConsoleWritePackets;
        private long _sizeOfConsoleWritePackets;

        /// <summary>
        /// Capture configuration of Client Console.
        /// </summary>
        private TargetConsoleConfiguration? _consoleConfiguration;

        /// <summary>
        /// Incoming packet pump and redirection.
        /// </summary>
        private MSBuildClientPacketPump _packetPump = null!;

        /// <summary>
        /// PID of the server process this client launched (or null if no launch was attempted /
        /// the server was already running). Used for diagnostics on connection failure.
        /// </summary>
        private int? _launchedServerPid;

        /// <summary>
        /// Whether this build is multithreaded (/mt). Determines whether the launched server process
        /// gets Server GC (the server only does in-process project work under /mt).
        /// </summary>
        private readonly bool _multiThreaded;

        /// <summary>
        /// Whether the server should shut itself down once this build completes instead of staying resident for reuse. 
        /// </summary>
        private readonly bool _shutdownServerAfterBuild;

        /// <summary>
        /// Public constructor with parameters.
        /// </summary>
        /// <param name="commandLine">The command line to process. The first argument
        /// on the command line is assumed to be the name/path of the executable, and is ignored</param>
        /// <param name="msbuildLocation"> Full path to current MSBuild.exe if executable is MSBuild.exe,
        /// or to version of MSBuild.dll found to be associated with the current process.</param>
        public MSBuildClient(string[] commandLine, string msbuildLocation)
            : this(commandLine, msbuildLocation, multiThreaded: false)
        {
        }

        /// <summary>
        /// Public constructor with parameters.
        /// </summary>
        /// <param name="commandLine">The command line to process. The first argument
        /// on the command line is assumed to be the name/path of the executable, and is ignored</param>
        /// <param name="msbuildLocation"> Full path to current MSBuild.exe if executable is MSBuild.exe,
        /// or to version of MSBuild.dll found to be associated with the current process.</param>
        /// <param name="multiThreaded">Whether this build is multithreaded (/mt). When true, the launched
        /// server process is started with Server GC.</param>
        public MSBuildClient(string[] commandLine, string msbuildLocation, bool multiThreaded)
            : this(commandLine, msbuildLocation, multiThreaded, shutdownServerAfterBuild: false)
        {
        }

        /// <summary>
        /// Public constructor with parameters.
        /// </summary>
        /// <param name="commandLine">The command line to process. The first argument
        /// on the command line is assumed to be the name/path of the executable, and is ignored</param>
        /// <param name="msbuildLocation"> Full path to current MSBuild.exe if executable is MSBuild.exe,
        /// or to version of MSBuild.dll found to be associated with the current process.</param>
        /// <param name="multiThreaded">Whether this build is multithreaded (/mt). When true, the launched
        /// server process is started with Server GC.</param>
        /// <param name="shutdownServerAfterBuild">Whether the server should shut itself down once this build
        /// completes instead of staying resident for reuse (e.g. a /mt build with -nodeReuse:false).</param>
        public MSBuildClient(string[] commandLine, string msbuildLocation, bool multiThreaded, bool shutdownServerAfterBuild)
        {
            _serverEnvironmentVariables = new();
            _exitResult = new();

            // dll & exe locations
            _commandLine = commandLine;
            _msbuildLocation = msbuildLocation;
            _multiThreaded = multiThreaded;
            _shutdownServerAfterBuild = shutdownServerAfterBuild;

            // Client <-> Server communication stream
            _handshake = GetHandshake();
            _pipeName = OutOfProcServerNode.GetPipeName(_handshake);
            _packetMemoryStream = new MemoryStream();
            _binaryWriter = new BinaryWriter(_packetMemoryStream);

            CreateNodePipeStream();
        }

        private void CreateNodePipeStream()
        {
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
            _nodeStream = new NamedPipeClientStream(
                serverName: ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                | PipeOptions.CurrentUserOnly
#endif
            );
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter
            _packetPump = new MSBuildClientPacketPump(_nodeStream);
        }

        /// <summary>
        /// Orchestrates the execution of the build on the server,
        /// responsible for client-server communication.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A value of type <see cref="MSBuildClientExitResult"/> that indicates whether the build succeeded,
        /// or the manner in which it failed.</returns>
        public MSBuildClientExitResult Execute(CancellationToken cancellationToken)
        {
            // Command line in one string used only in human readable content.
            string descriptiveCommandLine = string.Join(" ", _commandLine);

            CommunicationsUtilities.Trace($"Executing build with command line '{descriptiveCommandLine}'");

            try
            {
                bool serverIsAlreadyRunning = ServerIsRunning();
                if (KnownTelemetry.PartialBuildTelemetry != null)
                {
                    KnownTelemetry.PartialBuildTelemetry.InitialMSBuildServerState = serverIsAlreadyRunning ? "hot" : "cold";
                }
                if (!serverIsAlreadyRunning)
                {
                    CommunicationsUtilities.Trace("Server was not running. Starting server now.");
                    if (!TryLaunchServer())
                    {
                        _exitResult.MSBuildClientExitType = (_exitResult.MSBuildClientExitType == MSBuildClientExitType.Success) ? MSBuildClientExitType.LaunchError : _exitResult.MSBuildClientExitType;
                        return _exitResult;
                    }
                }

                // Check that server is not busy.
                bool serverWasBusy = ServerWasBusy();
                if (serverWasBusy)
                {
                    CommunicationsUtilities.Trace("Server is busy, falling back to former behavior.");
                    _exitResult.MSBuildClientExitType = MSBuildClientExitType.ServerBusy;
                    return _exitResult;
                }

                // Connect to server.
                if (!TryConnectToServer(serverIsAlreadyRunning ? 1_000 : 20_000))
                {
                    return _exitResult;
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex) && ex is not PathTooLongException)
            {
                // In unexpected state fall back to non-server execution.
                CommunicationsUtilities.Trace($"Failed to obtain the current build server state: {ex}");
                CommunicationsUtilities.Trace($"HResult: {ex.HResult}.");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.UnknownServerState;
                return _exitResult;
            }

            ConfigureAndQueryConsoleProperties();

            // Send build command.
            // Let's send it outside the packet pump so that we easier and quicker deal with possible issues with connection to server.
            MSBuildEventSource.Log.MSBuildServerBuildStart(descriptiveCommandLine);
            if (TrySendBuildCommand())
            {
                _numConsoleWritePackets = 0;
                _sizeOfConsoleWritePackets = 0;

                ReadPacketsLoop(cancellationToken);

                MSBuildEventSource.Log.MSBuildServerBuildStop(descriptiveCommandLine, _numConsoleWritePackets, _sizeOfConsoleWritePackets, _exitResult.MSBuildClientExitType.ToString(), _exitResult.MSBuildAppExitTypeString ?? string.Empty);
                CommunicationsUtilities.Trace("Build finished.");
            }

            NativeMethodsShared.RestoreConsoleMode(_originalConsoleMode);

            return _exitResult;
        }

        /// <summary>
        /// Attempt to shutdown MSBuild Server node.
        /// </summary>
        /// <remarks>
        /// It shutdown only server created by current user with current admin elevation.
        /// </remarks>
        /// <param name="cancellationToken"></param>
        /// <returns>True if server is not running anymore.</returns>
        public static bool ShutdownServer(CancellationToken cancellationToken)
        {
            // Neither commandLine nor msbuildlocation is involved in node shutdown
            var client = new MSBuildClient(commandLine: null!, msbuildLocation: null!);

            return client.TryShutdownServer(cancellationToken);
        }

        private bool TryShutdownServer(CancellationToken cancellationToken)
        {
            CommunicationsUtilities.Trace("Trying shutdown server node.");

            bool serverIsAlreadyRunning = ServerIsRunning();
            if (!serverIsAlreadyRunning)
            {
                CommunicationsUtilities.Trace("No need to shutdown server node for it is not running.");
                return true;
            }

            // Check and wait for server to be not busy for some short time to avoid race condition when server reports build is finished but had not released ServerBusy mutex yet.
            // If during that short time, a script would try to shutdown server, it would be rejected and server would continue to run.
            bool serverIsBusy = ServerIsBusyWithWaitAndRetry(250);
            if (serverIsBusy)
            {
                CommunicationsUtilities.Trace("Server cannot be shut down for it is not idle.");
                return false;
            }

            // Connect to server.
            if (!TryConnectToServer(1_000))
            {
                CommunicationsUtilities.Trace("Client cannot connect to idle server to shut it down.");
                return false;
            }

            if (!TrySendShutdownCommand())
            {
                CommunicationsUtilities.Trace("Failed to send shutdown command to the server.");
                return false;
            }

            ReadPacketsLoop(cancellationToken);

            return _exitResult.MSBuildClientExitType == MSBuildClientExitType.Success;
        }

        private bool ServerIsBusyWithWaitAndRetry(int milliseconds)
        {
            bool isBusy = ServerWasBusy();
            Stopwatch sw = Stopwatch.StartNew();
            while (isBusy && sw.ElapsedMilliseconds < milliseconds)
            {
                CommunicationsUtilities.Trace("Wait for server to be not busy - will retry soon...");
                Thread.Sleep(100);
                isBusy = ServerWasBusy();
            }

            return isBusy;
        }

        internal bool ServerIsRunning()
        {
            string serverRunningMutexName = OutOfProcServerNode.GetRunningServerMutexName(_handshake);
            bool serverIsAlreadyRunning = ServerNamedMutex.WasOpen(serverRunningMutexName);
            return serverIsAlreadyRunning;
        }

        private bool ServerWasBusy()
        {
            string serverBusyMutexName = OutOfProcServerNode.GetBusyServerMutexName(_handshake);
            var serverWasBusy = ServerNamedMutex.WasOpen(serverBusyMutexName);
            return serverWasBusy;
        }

        private void ReadPacketsLoop(CancellationToken cancellationToken)
        {
            try
            {
                // Start packet pump
                using MSBuildClientPacketPump packetPump = _packetPump;

                packetPump.RegisterPacketHandler(NodePacketType.ServerNodeConsoleWrite, ServerNodeConsoleWrite.FactoryForDeserialization, packetPump);
                packetPump.RegisterPacketHandler(NodePacketType.ServerNodeBuildResult, ServerNodeBuildResult.FactoryForDeserialization, packetPump);
                packetPump.Start();

                ProcessPacketsUntilBuildFinished(packetPump, cancellationToken);
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace($"MSBuild client error: problem during packet handling occurred: {ex}.");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.Unexpected;
            }
        }

        /// <summary>
        /// Consumes packets produced by <paramref name="packetPump"/> until the build finishes (the
        /// <see cref="ServerNodeBuildResult"/> is processed), the pump completes, or cancellation is requested.
        /// </summary>
        private void ProcessPacketsUntilBuildFinished(MSBuildClientPacketPump packetPump, CancellationToken cancellationToken)
        {
            WaitHandle[] waitHandles =
            {
                cancellationToken.WaitHandle,
                packetPump.PacketPumpCompleted,
                packetPump.PacketReceivedEvent
            };

            while (!_buildFinished)
            {
                int index = WaitHandle.WaitAny(waitHandles);
                switch (index)
                {
                    case 0:
                        HandleCancellation();
                        // After the cancelation, we want to wait to server gracefuly finish the build.
                        // We have to replace the cancelation handle, because WaitAny would cause to repeatedly hit this branch of code.
                        waitHandles[0] = CancellationToken.None.WaitHandle;
                        break;

                    case 1:
                        // The packet pump signals PacketPumpCompleted (a sticky ManualResetEvent at a
                        // lower WaitAny index than PacketReceivedEvent) immediately after enqueuing the
                        // final ServerNodeBuildResult. Drain any packets it enqueued right before
                        // completing - that result plus trailing console writes such as the
                        // "Build succeeded." summary - before treating the pump as finished. Otherwise a
                        // race where WaitAny observes index 1 before the queue is drained would drop the
                        // build result and exit 1 on a successful build (#14172).
                        DrainPacketQueue(packetPump);
                        if (!_buildFinished)
                        {
                            HandlePacketPumpCompleted(packetPump);
                        }

                        break;

                    case 2:
                        DrainPacketQueue(packetPump);
                        break;
                }
            }
        }

        /// <summary>
        /// Test hook for the #14172 regression: drives <see cref="ProcessPacketsUntilBuildFinished"/> against a
        /// pump whose received-packets queue and completion event have already been seeded, without needing a
        /// live server pipe, and returns the resulting <see cref="MSBuildClientExitResult"/>.
        /// </summary>
        internal MSBuildClientExitResult ProcessSeededPacketsForTests(MSBuildClientPacketPump packetPump)
        {
            ProcessPacketsUntilBuildFinished(packetPump, CancellationToken.None);
            return _exitResult;
        }

        private void ConfigureAndQueryConsoleProperties()
        {
            (var acceptAnsiColorCodes, var outputIsScreen, _originalConsoleMode) = NativeMethodsShared.QueryIsScreenAndTryEnableAnsiColorCodes();
            int bufferWidth = QueryConsoleBufferWidth();
            ConsoleColor backgroundColor = QueryConsoleBackgroundColor();

            _consoleConfiguration = new TargetConsoleConfiguration(bufferWidth, acceptAnsiColorCodes, outputIsScreen, backgroundColor);
        }

        private int QueryConsoleBufferWidth()
        {
            int consoleBufferWidth = -1;
            try
            {
                consoleBufferWidth = Console.BufferWidth;
            }
            catch (Exception ex)
            {
                // on Win8 machines while in IDE Console.BufferWidth will throw (while it talks to native console it gets "operation aborted" native error)
                // this is probably temporary workaround till we understand what is the reason for that exception
                CommunicationsUtilities.Trace($"MSBuild client warning: problem during querying console buffer width: {ex}");
            }

            return consoleBufferWidth;
        }

        /// <summary>
        /// Some platforms do not allow getting current background color. There
        /// is not way to check, but not-supported exception is thrown. Assume
        /// black, but don't crash.
        /// </summary>
        private ConsoleColor QueryConsoleBackgroundColor()
        {
            ConsoleColor consoleBackgroundColor;
            try
            {
                consoleBackgroundColor = Console.BackgroundColor;
            }
            catch (PlatformNotSupportedException)
            {
                consoleBackgroundColor = ConsoleColor.Black;
            }

            return consoleBackgroundColor;
        }

        private bool TrySendPacket(Func<INodePacket> packetResolver)
        {
            INodePacket? packet = null;
            try
            {
                packet = packetResolver();
                WritePacket(_nodeStream, packet);
                CommunicationsUtilities.Trace($"Command packet of type '{packet.Type}' sent...");
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace($"Failed to send command packet of type '{packet?.Type.ToString() ?? "Unknown"}' to server: {ex}");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.Unexpected;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Launches MSBuild server.
        /// </summary>
        /// <returns> Whether MSBuild server was started successfully.</returns>
        private bool TryLaunchServer()
        {
            string serverLaunchMutexName = $@"Global\msbuild-server-launch-{_handshake.ComputeHash()}";

            try
            {
                using var serverLaunchMutex = ServerNamedMutex.OpenOrCreateMutex(serverLaunchMutexName, out bool mutexCreatedNew);

                if (!mutexCreatedNew)
                {
                    // Some other client process launching a server and setting a build request for it. Fallback to usual msbuild app build.
                    CommunicationsUtilities.Trace("Another process launching the msbuild server, falling back to former behavior.");
                    _exitResult.MSBuildClientExitType = MSBuildClientExitType.ServerBusy;
                    return false;
                }
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex) && ex is not PathTooLongException)
            {
                // In unexpected state fall back to non-server execution.
                CommunicationsUtilities.Trace($"Failed to acquire server launch mutex '{serverLaunchMutexName}': {ex}");
                CommunicationsUtilities.Trace($"HResult: {ex.HResult}.");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.UnknownServerState;
                return false;
            }

            try
            {
                string[] msBuildServerOptions =
                [
                    "/nologo",
                    NodeModeHelper.ToCommandLineArgument(NodeMode.OutOfProcServerNode)
                ];
                NodeLauncher nodeLauncher = new NodeLauncher();
                CommunicationsUtilities.Trace("Starting Server...");

                // Set DOTNET_ROOT so the apphost server child can locate the runtime; this
                // override is replaced by the client's environment on the first build command
                // (see OutOfProcServerNode.HandleServerNodeBuildCommand → SetEnvironment).
                // This is a shared cached dictionary reused by other node launches, so it must not
                // be mutated in place.
                IDictionary<string, string?>? baseOverrides = DotnetHostEnvironmentHelper.CreateDotnetRootEnvironmentOverrides();
                IDictionary<string, string?>? environmentOverrides = baseOverrides;

                // When this build is multithreaded (/mt), launch the long-lived server (build
                // orchestrator) process with Server GC. Other processes may get higher throughput
                // with Server GC, but we want to enable it only for the highest-impact one where
                // most of the work of the actual build will happen in MT mode. GC mode is fixed at
                // CLR startup, so it must be set in the child's launch environment via DOTNET_gcServer
                // (which only affects .NET/CoreCLR and, since .NET 9, takes precedence over
                // runtimeconfig.json). The decision is made from this (launch-time) invocation's command
                // line; a server is keyed by handshake and reused, so a later differing /mt choice does
                // not re-launch it.
                //
                // This is scoped to the server process only. Sidecar TaskHost (nodemode 2) and worker
                // (nodemode 1) nodes are launched through other code paths that never set this knob, and
                // the server resets its own environment to the client's on the first build command (so
                // DOTNET_gcServer is removed before it ever spawns children). Those nodes therefore keep
                // the default Workstation GC.
                //
                // Copy the shared base overrides (preserving its comparer) before adding the GC override.
                // Honor an explicit user-set DOTNET_gcServer (e.g. "0" to force Workstation GC in a
                // memory-constrained container): only inject the default when the user hasn't set it.
                if (_multiThreaded &&
                    Environment.GetEnvironmentVariable("DOTNET_gcServer") is null)
                {
                    environmentOverrides = baseOverrides is null
                        ? new Dictionary<string, string?>()
                        : new Dictionary<string, string?>(baseOverrides);
                    environmentOverrides["DOTNET_gcServer"] = "1";
                }

                NodeLaunchData launchData = new(
                    MSBuildLocation: _msbuildLocation,
                    CommandLineArgs: string.Join(" ", msBuildServerOptions),
                    EnvironmentOverrides: environmentOverrides);

                using Process msbuildProcess = nodeLauncher.Start(launchData, nodeId: 0);
                _launchedServerPid = msbuildProcess.Id;
                CommunicationsUtilities.Trace($"Server started with PID: {_launchedServerPid}");
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace($"Failed to launch the msbuild server: {ex}");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.LaunchError;
                return false;
            }

            return true;
        }

        private bool TrySendBuildCommand() => TrySendPacket(() => GetServerNodeBuildCommand());

        private bool TrySendCancelCommand() => TrySendPacket(() => new ServerNodeBuildCancel());

        private bool TrySendShutdownCommand()
        {
            CommunicationsUtilities.Trace("Sending shutdown command to server.");
            _packetPump.ServerWillDisconnect();
            return TrySendPacket(() => new NodeBuildComplete(false /* no node reuse */));
        }

        private ServerNodeBuildCommand GetServerNodeBuildCommand()
        {
            Dictionary<string, string> envVars = new();

            foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables())
            {
                envVars[(string)envVar.Key] = (envVar.Value as string) ?? string.Empty;
            }

            foreach (var pair in _serverEnvironmentVariables)
            {
                envVars[pair.Key] = pair.Value;
            }

            // We remove env variable used to invoke MSBuild server as that might be equal to 1, so we do not get an infinite recursion here.
            envVars.Remove(Traits.UseMSBuildServerEnvVarName);

            Debug.Assert(KnownTelemetry.PartialBuildTelemetry == null || KnownTelemetry.PartialBuildTelemetry.StartAt.HasValue, "BuildTelemetry.StartAt was not initialized!");

            PartialBuildTelemetry? partialBuildTelemetry = KnownTelemetry.PartialBuildTelemetry == null
                ? null
                : new PartialBuildTelemetry(
                    startedAt: KnownTelemetry.PartialBuildTelemetry.StartAt.GetValueOrDefault(),
                    initialServerState: KnownTelemetry.PartialBuildTelemetry.InitialMSBuildServerState,
                    serverFallbackReason: KnownTelemetry.PartialBuildTelemetry.ServerFallbackReason,
                    serverEnableReason: KnownTelemetry.PartialBuildTelemetry.ServerEnableReason);

            return new ServerNodeBuildCommand(
                        _commandLine,
                        startupDirectory: Directory.GetCurrentDirectory(),
                        buildProcessEnvironment: envVars,
                        CultureInfo.CurrentCulture,
                        CultureInfo.CurrentUICulture,
                        _consoleConfiguration!,
                        partialBuildTelemetry,
                        _shutdownServerAfterBuild);
        }

        private ServerNodeHandshake GetHandshake() => new(CommunicationsUtilities.GetHandshakeOptions(
            taskHost: false,
            taskHostParameters: TaskHostParameters.Empty,
            architectureFlagToSet: XMakeAttributes.GetCurrentMSBuildArchitecture()));

        /// <summary>
        /// Handle cancellation.
        /// </summary>
        private void HandleCancellation()
        {
            TrySendCancelCommand();

            CommunicationsUtilities.Trace("MSBuild client sent cancellation command.");
        }

        /// <summary>
        /// Handle when packet pump is completed both successfully or with error.
        /// </summary>
        private void HandlePacketPumpCompleted(MSBuildClientPacketPump packetPump)
        {
            if (packetPump.PacketPumpException != null)
            {
                CommunicationsUtilities.Trace($"MSBuild client error: packet pump unexpectedly shut down: {packetPump.PacketPumpException}");
                throw packetPump.PacketPumpException ?? new InternalErrorException("Packet pump unexpectedly shut down");
            }

            _buildFinished = true;
        }

        /// <summary>
        /// Processes every packet currently sitting in the packet pump's received queue, stopping early
        /// once the build result has been handled. Shared by the PacketReceivedEvent and
        /// PacketPumpCompleted branches so that packets the pump enqueues immediately before it completes
        /// (notably the final <see cref="ServerNodeBuildResult"/> and trailing console output) are never
        /// dropped by an event-ordering race. See https://github.com/dotnet/msbuild/issues/14172.
        /// </summary>
        private void DrainPacketQueue(MSBuildClientPacketPump packetPump)
        {
            while (packetPump.ReceivedPacketsQueue.TryDequeue(out INodePacket? packet) &&
                   !_buildFinished)
            {
                if (packet != null)
                {
                    HandlePacket(packet);
                }
            }
        }

        /// <summary>
        /// Dispatches the packet to the correct handler.
        /// </summary>
        private void HandlePacket(INodePacket packet)
        {
            switch (packet.Type)
            {
                case NodePacketType.ServerNodeConsoleWrite:
                    ServerNodeConsoleWrite writePacket = (packet as ServerNodeConsoleWrite)!;
                    HandleServerNodeConsoleWrite(writePacket);
                    _numConsoleWritePackets++;
                    _sizeOfConsoleWritePackets += writePacket.Text.Length;
                    break;
                case NodePacketType.ServerNodeBuildResult:
                    HandleServerNodeBuildResult((ServerNodeBuildResult)packet);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected packet type {packet.GetType().Name}");
            }
        }

        private void HandleServerNodeConsoleWrite(ServerNodeConsoleWrite consoleWrite)
        {
            switch (consoleWrite.OutputType)
            {
                case ConsoleOutput.Standard:
                    Console.Write(consoleWrite.Text);
                    break;
                case ConsoleOutput.Error:
                    Console.Error.Write(consoleWrite.Text);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected console output type {consoleWrite.OutputType}");
            }
        }

        private void HandleServerNodeBuildResult(ServerNodeBuildResult response)
        {
            CommunicationsUtilities.Trace($"Build response received: exit code '{response.ExitCode}', exit type '{response.ExitType}'");
            _exitResult.MSBuildClientExitType = MSBuildClientExitType.Success;
            _exitResult.MSBuildAppExitTypeString = response.ExitType;
            _buildFinished = true;
        }

        /// <summary>
        /// Connects to MSBuild server.
        /// </summary>
        /// <returns> Whether the client connected to MSBuild server successfully.</returns>
        private bool TryConnectToServer(int timeoutMilliseconds)
        {
            bool tryAgain = true;
            Stopwatch sw = Stopwatch.StartNew();

            while (tryAgain && sw.ElapsedMilliseconds < timeoutMilliseconds)
            {
                tryAgain = false;

                HandshakeResult result;
                bool connected;
                try
                {
                    connected = NodeProviderOutOfProcBase.TryConnectToPipeStream(
                        _nodeStream, _pipeName, _handshake, Math.Max(1, timeoutMilliseconds - (int)sw.ElapsedMilliseconds), out result);
                }
                catch (TimeoutException)
                {
                    // The underlying NamedPipeClientStream.Connect throws TimeoutException when the
                    // pipe never becomes available — typically because the server child process
                    // failed to start (e.g. apphost couldn't locate the runtime). Treat this as a
                    // recoverable connection failure so MSBuildClientApp can fall back to in-proc
                    // execution rather than crashing the whole CLI.
                    LogConnectFailureDiagnostics(timeoutMilliseconds, isTimeout: true, errorMessage: null);
                    _exitResult.MSBuildClientExitType = MSBuildClientExitType.UnableToConnect;
                    return false;
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    // Mirror the exception-tolerant behavior of NodeProviderOutOfProcBase.TryConnectToProcess
                    // so any non-critical failure (UnauthorizedAccessException, IOException,
                    // InvalidOperationException, etc.) routes through the standard fallback path
                    // rather than escaping out of MSBuildClient.Execute.
                    LogConnectFailureDiagnostics(timeoutMilliseconds, isTimeout: false, errorMessage: ex.Message);
                    _exitResult.MSBuildClientExitType = MSBuildClientExitType.UnableToConnect;
                    return false;
                }

                if (connected)
                {
                    return true;
                }
                else
                {
                    if (result.Status is not HandshakeStatus.Timeout && sw.ElapsedMilliseconds < timeoutMilliseconds)
                    {
                        CommunicationsUtilities.Trace($"Retrying to connect to server after {sw.ElapsedMilliseconds} ms");
                        // This solves race condition for time in which server started but have not yet listen on pipe or
                        // when it just finished build request and is recycling pipe.
                        tryAgain = true;
                        CreateNodePipeStream();
                    }
                    else
                    {
                        LogConnectFailureDiagnostics(timeoutMilliseconds, isTimeout: result.Status is HandshakeStatus.Timeout, errorMessage: result.ErrorMessage);
                        _exitResult.MSBuildClientExitType = MSBuildClientExitType.UnableToConnect;
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Emits a single diagnostic trace entry describing why connection to the MSBuild server
        /// failed, including the launched server PID (if any) and its current state. This makes
        /// the otherwise-opaque 20s timeout actionable when MSBUILDDEBUGCOMM tracing is enabled.
        /// Also populates <see cref="MSBuildClientExitResult.ServerProcessExitCode"/> when the
        /// launched server child has already exited, so the host can surface that fact to the
        /// user-visible "falling back to in-proc" message instead of a generic timeout.
        /// </summary>
        private void LogConnectFailureDiagnostics(int timeoutMilliseconds, bool isTimeout, string? errorMessage)
        {
            string serverState;
            if (_launchedServerPid is int pid)
            {
                try
                {
                    using Process launched = Process.GetProcessById(pid);
                    if (launched.HasExited)
                    {
                        _exitResult.ServerProcessExitCode = launched.ExitCode;
                        serverState = $"PID {pid} (already exited with code {launched.ExitCode})";
                    }
                    else
                    {
                        serverState = $"PID {pid} (still running)";
                    }
                }
                catch (ArgumentException)
                {
                    // Process already terminated and was reaped before we could query it.
                    serverState = $"PID {pid} (already exited)";
                }
                catch (InvalidOperationException)
                {
                    serverState = $"PID {pid} (state unavailable)";
                }
            }
            else
            {
                serverState = "no launch attempted (server reported as already running)";
            }

            string reason = isTimeout
                ? $"timed out after {timeoutMilliseconds} ms waiting for the named pipe"
                : $"connection error: {errorMessage}";

            CommunicationsUtilities.Trace(
                $"MSBuild server connection failed ({reason}). Launched server: {serverState}. " +
                "Falling back to in-proc build. " +
                "If the server child process exited immediately, ensure DOTNET_ROOT is set correctly so the apphost can locate the .NET runtime.");
        }

        private void WritePacket(Stream nodeStream, INodePacket packet)
        {
            MemoryStream memoryStream = _packetMemoryStream;
            memoryStream.SetLength(0);

            ITranslator writeTranslator = BinaryTranslator.GetWriteTranslator(memoryStream);

            // Write header
            memoryStream.WriteByte((byte)packet.Type);

            // Pad for packet length
            _binaryWriter.Write(0);

            // Reset the position in the write buffer.
            packet.Translate(writeTranslator);

            int packetStreamLength = (int)memoryStream.Position;

            // Now write in the actual packet length
            memoryStream.Position = 1;
            _binaryWriter.Write(packetStreamLength - 5);

            nodeStream.Write(memoryStream.GetBuffer(), 0, packetStreamLength);
        }
    }
}
