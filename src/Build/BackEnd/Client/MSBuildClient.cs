// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
using Microsoft.Build.Eventing;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using BackendNativeMethods = Microsoft.Build.BackEnd.NativeMethods;
using NativeMethods = Microsoft.Build.BackEnd.NativeMethods;

#nullable disable

namespace Microsoft.Build.Execution
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
        /// Location of executable file to launch the server process. That should be either dotnet.exe or MSBuild.exe location.
        /// </summary>
        private readonly string _exeLocation;

        /// <summary>
        /// Location of dll file to launch the server process if needed. Empty if executable is msbuild.exe and not empty if dotnet.exe.
        /// </summary>
        private readonly string _dllLocation;

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
        private readonly NamedPipeClientStream _nodeStream;

        /// <summary>
        /// A way to cache a byte array when writing out packets
        /// </summary>
        private readonly MemoryStream _packetMemoryStream;

        /// <summary>
        /// A binary writer to help write into <see cref="_packetMemoryStream"/>
        /// </summary>
        private readonly BinaryWriter _binaryWriter;

        /// <summary>
        /// Public constructor with parameters.
        /// </summary>
        /// <param name="exeLocation">Location of executable file to launch the server process.
        /// That should be either dotnet.exe or MSBuild.exe location.</param>
        /// <param name="dllLocation">Location of dll file to launch the server process if needed.
        /// Empty if executable is msbuild.exe and not empty if dotnet.exe.</param>
        public MSBuildClient(string exeLocation, string dllLocation)
        {
            _serverEnvironmentVariables = new();
            _exitResult = new();

            // dll & exe locations
            _exeLocation = exeLocation;
            _dllLocation = dllLocation;

            // Client <-> Server communication stream
            _handshake = GetHandshake();
            _pipeName = OutOfProcServerNode.GetPipeName(_handshake);
            _nodeStream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                                                                         | PipeOptions.CurrentUserOnly
#endif
            );

            _packetMemoryStream = new MemoryStream();
            _binaryWriter = new BinaryWriter(_packetMemoryStream);
        }

        /// <summary>
        /// Orchestrates the execution of the build on the server,
        /// responsible for client-server communication.
        /// </summary>
        /// <param name="commandLine">The command line to process. The first argument
        /// on the command line is assumed to be the name/path of the executable, and
        /// is ignored.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A value of type <see cref="MSBuildClientExitResult"/> that indicates whether the build succeeded,
        /// or the manner in which it failed.</returns>
        public MSBuildClientExitResult Execute(string commandLine, CancellationToken cancellationToken)
        {
            string serverRunningMutexName = OutOfProcServerNode.GetRunningServerMutexName(_handshake);
            string serverBusyMutexName = OutOfProcServerNode.GetBusyServerMutexName(_handshake);

            // Start server it if is not running.
            bool serverIsAlreadyRunning = ServerNamedMutex.WasOpen(serverRunningMutexName);
            if (!serverIsAlreadyRunning && !TryLaunchServer())
            {
                return _exitResult;
            }

            // Check that server is not busy.
            var serverWasBusy = ServerNamedMutex.WasOpen(serverBusyMutexName);
            if (serverWasBusy)
            {
                CommunicationsUtilities.Trace("Server is busy, falling back to former behavior.");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.ServerBusy;
                return _exitResult;
            }

            // Connect to server.
            if (!TryConnectToServer(serverIsAlreadyRunning ? 1_000 : 20_000))
            {
                CommunicationsUtilities.Trace("Failure to connect to a server.");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.ConnectionError;
                return _exitResult;
            }

            // Send build command.
            // Let's send it outside the packet pump so that we easier and quicker deal with possible issues with connection to server.
            if (!TrySendBuildCommand(commandLine))
            {
                CommunicationsUtilities.Trace("Failure to connect to a server.");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.ConnectionError;
                return _exitResult;
            }

            try
            {
                // Start packet pump
                using MSBuildClientPacketPump packetPump = new(_nodeStream);

                packetPump.RegisterPacketHandler(NodePacketType.ServerNodeConsoleWrite, ServerNodeConsoleWrite.FactoryForDeserialization, packetPump);
                packetPump.RegisterPacketHandler(NodePacketType.ServerNodeBuildResult, ServerNodeBuildResult.FactoryForDeserialization, packetPump);
                packetPump.Start();

                WaitHandle[] waitHandles =
                {
                    cancellationToken.WaitHandle,
                    packetPump.PacketPumpErrorEvent,
                    packetPump.PacketReceivedEvent
                };

                while (!_buildFinished)
                {
                    int index = WaitHandle.WaitAny(waitHandles);
                    switch (index)
                    {
                        case 0:
                            HandleCancellation();
                            break;

                        case 1:
                            HandlePacketPumpError(packetPump);
                            break;

                        case 2:
                            while (packetPump.ReceivedPacketsQueue.TryDequeue(out INodePacket packet) &&
                                   !_buildFinished &&
                                   !cancellationToken.IsCancellationRequested)
                            {
                                if (packet != null)
                                {
                                    HandlePacket(packet);
                                }
                            }

                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace("MSBuild client error: problem during packet handling occurred: {0}.", ex);
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.Unexpected;
            }

            CommunicationsUtilities.Trace("Build finished.");
            return _exitResult;
        }

        private void SendCancelCommand(NamedPipeClientStream nodeStream) => throw new NotImplementedException();

        /// <summary>
        /// Launches MSBuild server. 
        /// </summary>
        /// <returns> Whether MSBuild server was started successfully.</returns>
        private bool TryLaunchServer()
        {
            string serverLaunchMutexName = $@"Global\server-launch-{_handshake.ComputeHash()}";
            using var serverLaunchMutex = ServerNamedMutex.OpenOrCreateMutex(serverLaunchMutexName, out bool mutexCreatedNew);
            if (!mutexCreatedNew)
            {
                // Some other client process launching a server and setting a build request for it. Fallback to usual msbuild app build.
                CommunicationsUtilities.Trace("Another process launching the msbuild server, falling back to former behavior.");
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.ServerBusy;
                return false;
            }

            // Temporary hack
            string[] msBuildServerOptions = new string[] {
                // _dllLocation,
                "/nologo",
                "/nodemode:8"
            };

            string msbuildLocation;
            if (string.IsNullOrEmpty(_dllLocation))
            {
                msbuildLocation = _exeLocation;
            }
            else
            {
                msbuildLocation = _dllLocation;
            }

            try
            {
                Process msbuildProcess = LaunchNode(msbuildLocation, string.Join(" ", msBuildServerOptions), _serverEnvironmentVariables);
                CommunicationsUtilities.Trace("Server is launched with PID: {0}", msbuildProcess?.Id);
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace("Failed to launch the msbuild server: {0}", ex);
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.LaunchError;
                return false;
            }

            return true;
        }

        /* 

                private Process? LaunchNode(string exeLocation, string msBuildServerArguments, Dictionary<string, string> serverEnvironmentVariables)
                {
                    CommunicationsUtilities.Trace("Launching server node from {0} with arguments {1}", exeLocation, msBuildServerArguments);

                    BackendNativeMethods.STARTUP_INFO startInfo = new();
                    startInfo.cb = Marshal.SizeOf<BackendNativeMethods.STARTUP_INFO>();

                    // Null out the process handles so that the parent process does not wait for the child process
                    // to exit before it can exit.
                    uint creationFlags = 0;
                    if (Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout)
                    {
                        creationFlags = BackendNativeMethods.NORMALPRIORITYCLASS;
                    }

                    if (!NativeMethodsShared.IsWindows)
                    {
                        ProcessStartInfo processStartInfo = new()
                        {
                            FileName = exeLocation,
                            Arguments = msBuildServerArguments,
                            UseShellExecute = false
                        };

                        foreach (var entry in serverEnvironmentVariables)
                        {
                            processStartInfo.Environment[entry.Key] = entry.Value;
                        }

                        // We remove env to enable MSBuild Server that might be equal to 1, so we do not get an infinite recursion here.
                        processStartInfo.Environment[Traits.UseMSBuildServerEnvVarName] = "0";

                        processStartInfo.CreateNoWindow = true;
                        processStartInfo.UseShellExecute = false;

                        // Redirect the streams of worker nodes so that this MSBuild.exe's
                        // parent doesn't wait on idle worker nodes to close streams
                        // after the build is complete.
                        processStartInfo.RedirectStandardInput = true;
                        processStartInfo.RedirectStandardOutput = true;
                        processStartInfo.RedirectStandardError = true;

                        Process? process = null;
                        try
                        {
                            process = Process.Start(processStartInfo);
                        }
                        catch (Exception ex)
                        {
                            CommunicationsUtilities.Trace
                               (
                                   "Failed to launch server node from {0}. CommandLine: {1}" + Environment.NewLine + "{2}",
                                   exeLocation,
                                   msBuildServerArguments,
                                   ex.ToString()
                               );

                            throw new NodeFailedToLaunchException(ex);
                        }

                        CommunicationsUtilities.Trace("Successfully launched server node with PID {0}", process?.Id);
                        return process;
                    }
                    else
                    {
                        // TODO: IT DOES NOT USE EXTRA ENV VARIABLES!!!

                        BackendNativeMethods.PROCESS_INFORMATION processInfo = new();
                        BackendNativeMethods.SECURITY_ATTRIBUTES processSecurityAttributes = new();
                        BackendNativeMethods.SECURITY_ATTRIBUTES threadSecurityAttributes = new();
                        processSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();
                        threadSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();

                        bool result = false;
                        try
                        {
                            Environment.SetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName, "0");
                            result = BackendNativeMethods.CreateProcess
                           (
                               exeLocation,
                               msBuildServerArguments,
                               ref processSecurityAttributes,
                               ref threadSecurityAttributes,
                               false,
                               creationFlags,
                               BackendNativeMethods.NullPtr,
                               null,
                               ref startInfo,
                               out processInfo
                           );
                        }
                        finally
                        {
                            Environment.SetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName, "1");
                        }


                        if (!result)
                        {
                            // Creating an instance of this exception calls GetLastWin32Error and also converts it to a user-friendly string.
                            System.ComponentModel.Win32Exception e = new System.ComponentModel.Win32Exception();

                            CommunicationsUtilities.Trace
                                (
                                    "Failed to launch node from {0}. System32 Error code {1}. Description {2}. CommandLine: {2}",
                                    exeLocation,
                                    e.NativeErrorCode.ToString(CultureInfo.InvariantCulture),
                                    e.Message,
                                    msBuildServerArguments
                                );

                            throw new NodeFailedToLaunchException(e.NativeErrorCode.ToString(CultureInfo.InvariantCulture), e.Message);
                        }

                        int childProcessId = processInfo.dwProcessId;

                        if (processInfo.hProcess != IntPtr.Zero && processInfo.hProcess != NativeMethods.InvalidHandle)
                        {
                            NativeMethodsShared.CloseHandle(processInfo.hProcess);
                        }

                        if (processInfo.hThread != IntPtr.Zero && processInfo.hThread != NativeMethods.InvalidHandle)
                        {
                            NativeMethodsShared.CloseHandle(processInfo.hThread);
                        }

                        CommunicationsUtilities.Trace("Successfully launched server node with PID {0}", childProcessId);
                        return Process.GetProcessById(childProcessId);
                    } 
                }
        */


#if RUNTIME_TYPE_NETCORE || MONO
        private static string CurrentHost;
#endif

        /// <summary>
        /// Identify the .NET host of the current process.
        /// </summary>
        /// <returns>The full path to the executable hosting the current process, or null if running on Full Framework on Windows.</returns>
        private static string GetCurrentHost()
        {
#if RUNTIME_TYPE_NETCORE || MONO
            if (CurrentHost == null)
            {
                string dotnetExe = Path.Combine(FileUtilities.GetFolderAbove(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, 2),
                    NativeMethodsShared.IsWindows ? "dotnet.exe" : "dotnet");
                if (File.Exists(dotnetExe))
                {
                    CurrentHost = dotnetExe;
                }
                else
                {
                    using (Process currentProcess = Process.GetCurrentProcess())
                    {
                        CurrentHost = currentProcess.MainModule.FileName;
                    }
                }
            }

            return CurrentHost;
#else
            return null;
#endif
        }

        private Process LaunchNode(string msbuildLocation, string commandLineArgs, Dictionary<string, string> serverEnvironmentVariables)
        {
            // Should always have been set already.
            ErrorUtilities.VerifyThrowInternalLength(msbuildLocation, nameof(msbuildLocation));

            if (!FileSystems.Default.FileExists(msbuildLocation))
            {
                throw new BuildAbortedException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CouldNotFindMSBuildExe", msbuildLocation));
            }

            // Repeat the executable name as the first token of the command line because the command line
            // parser logic expects it and will otherwise skip the first argument
            commandLineArgs = $"\"{msbuildLocation}\" {commandLineArgs}";

            BackendNativeMethods.STARTUP_INFO startInfo = new();
            startInfo.cb = Marshal.SizeOf<BackendNativeMethods.STARTUP_INFO>();

            // Null out the process handles so that the parent process does not wait for the child process
            // to exit before it can exit.
            uint creationFlags = 0;
            if (Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout)
            {
                FileName = exeLocation,
                Arguments = msBuildServerArguments,
                UseShellExecute = false
            };

            foreach (var entry in serverEnvironmentVariables)
            {
                processStartInfo.Environment[entry.Key] = entry.Value;
            }

            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDNODEWINDOW")))
            {
                if (!Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout)
                {
                    // Redirect the streams of worker nodes so that this MSBuild.exe's
                    // parent doesn't wait on idle worker nodes to close streams
                    // after the build is complete.
                    startInfo.hStdError = BackendNativeMethods.InvalidHandle;
                    startInfo.hStdInput = BackendNativeMethods.InvalidHandle;
                    startInfo.hStdOutput = BackendNativeMethods.InvalidHandle;
                    startInfo.dwFlags = BackendNativeMethods.STARTFUSESTDHANDLES;
                    creationFlags |= BackendNativeMethods.CREATENOWINDOW;
                }
            }
            else
            {
                creationFlags |= BackendNativeMethods.CREATE_NEW_CONSOLE;
            }

            CommunicationsUtilities.Trace("Launching node from {0}", msbuildLocation);

            string exeName = msbuildLocation;

#if RUNTIME_TYPE_NETCORE || MONO
            // Mono automagically uses the current mono, to execute a managed assembly
            if (!NativeMethodsShared.IsMono)
            {
                // Run the child process with the same host as the currently-running process.
                exeName = GetCurrentHost();
            }
#endif

            if (!NativeMethodsShared.IsWindows)
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = exeName;
                processStartInfo.Arguments = commandLineArgs;
                if (!Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout)
                {
                    // Redirect the streams of worker nodes so that this MSBuild.exe's
                    // parent doesn't wait on idle worker nodes to close streams
                    // after the build is complete.
                    processStartInfo.RedirectStandardInput = true;
                    processStartInfo.RedirectStandardOutput = true;
                    processStartInfo.RedirectStandardError = true;
                    processStartInfo.CreateNoWindow = (creationFlags | BackendNativeMethods.CREATENOWINDOW) == BackendNativeMethods.CREATENOWINDOW;
                }
                processStartInfo.UseShellExecute = false;


            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;

                // We remove env to enable MSBuild Server that might be equal to 1, so we do not get an infinite recursion here.
                processStartInfo.Environment[Traits.UseMSBuildServerEnvVarName] = "0";

                Process process;
                try
                {
                    process = Process.Start(processStartInfo);
                }
                catch (Exception ex)
                {
                    CommunicationsUtilities.Trace
                       (
                           "Failed to launch node from {0}. CommandLine: {1}" + Environment.NewLine + "{2}",
                           msbuildLocation,
                           commandLineArgs,
                           ex.ToString()
                       );

                    throw new NodeFailedToLaunchException(ex);
                }

                CommunicationsUtilities.Trace("Successfully launched {1} node with PID {0}", process.Id, exeName);
                return process;
            }
            else
            {
                // TODO: IT DOES NOT USE EXTRA ENV VARIABLES!!!

#if RUNTIME_TYPE_NETCORE
                // Repeat the executable name in the args to suit CreateProcess
                commandLineArgs = $"\"{exeName}\" {commandLineArgs}";
#endif

                BackendNativeMethods.PROCESS_INFORMATION processInfo = new();
                BackendNativeMethods.SECURITY_ATTRIBUTES processSecurityAttributes = new();
                BackendNativeMethods.SECURITY_ATTRIBUTES threadSecurityAttributes = new();
                processSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();
                threadSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();


                bool result = false;
                try
                {
                    Environment.SetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName, "0");
                    result = BackendNativeMethods.CreateProcess
                    (
                        exeName,
                        commandLineArgs,
                        ref processSecurityAttributes,
                        ref threadSecurityAttributes,
                        false,
                        creationFlags,
                        BackendNativeMethods.NullPtr,
                        null,
                        ref startInfo,
                        out processInfo
                    );
                }
                finally
                {
                    Environment.SetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName, "1");
                }


                if (!result)
                {
                    // Creating an instance of this exception calls GetLastWin32Error and also converts it to a user-friendly string.
                    System.ComponentModel.Win32Exception e = new System.ComponentModel.Win32Exception();

                    CommunicationsUtilities.Trace
                        (
                            "Failed to launch node from {0}. System32 Error code {1}. Description {2}. CommandLine: {2}",
                            msbuildLocation,
                            e.NativeErrorCode.ToString(CultureInfo.InvariantCulture),
                            e.Message,
                            commandLineArgs
                        );

                    throw new NodeFailedToLaunchException(e.NativeErrorCode.ToString(CultureInfo.InvariantCulture), e.Message);
                }

                int childProcessId = processInfo.dwProcessId;

                if (processInfo.hProcess != IntPtr.Zero && processInfo.hProcess != NativeMethods.InvalidHandle)
                {
                    NativeMethodsShared.CloseHandle(processInfo.hProcess);
                }

                if (processInfo.hThread != IntPtr.Zero && processInfo.hThread != NativeMethods.InvalidHandle)
                {
                    NativeMethodsShared.CloseHandle(processInfo.hThread);
                }

                CommunicationsUtilities.Trace("Successfully launched {1} node with PID {0}", childProcessId, exeName);
                return Process.GetProcessById(childProcessId);
            }
        }

        private bool TrySendBuildCommand(string commandLine)
        {
            try
            {
                ServerNodeBuildCommand buildCommand = GetServerNodeBuildCommand(commandLine);
                WritePacket(_nodeStream, buildCommand);
                CommunicationsUtilities.Trace("Build command send...");
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace("Failed to send build command to server: {0}", ex);
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.ConnectionError;
                return false;
            }

            return true;
        }

        private ServerNodeBuildCommand GetServerNodeBuildCommand(string commandLine)
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
            envVars[Traits.UseMSBuildServerEnvVarName] = "0";

            return new ServerNodeBuildCommand(
                        commandLine,
                        startupDirectory: Directory.GetCurrentDirectory(),
                        buildProcessEnvironment: envVars,
                        CultureInfo.CurrentCulture,
                        CultureInfo.CurrentUICulture);
        }

        private ServerNodeHandshake GetHandshake()
        {
            return new ServerNodeHandshake(CommunicationsUtilities.GetHandshakeOptions(taskHost: false, architectureFlagToSet: XMakeAttributes.GetCurrentMSBuildArchitecture()));
        }

        /// <summary>
        /// Handle cancellation.
        /// </summary>
        private void HandleCancellation()
        {
            // TODO.
            // Send cancellation command to server.
            // SendCancelCommand(_nodeStream);

            Console.WriteLine("MSBuild client cancelled.");
            CommunicationsUtilities.Trace("MSBuild client cancelled.");
            _exitResult.MSBuildClientExitType = MSBuildClientExitType.Cancelled;
            _buildFinished = true;
        }

        /// <summary>
        /// Handle packet pump error.
        /// </summary>
        private void HandlePacketPumpError(MSBuildClientPacketPump packetPump)
        {
            CommunicationsUtilities.Trace("MSBuild client error: packet pump unexpectedly shut down: {0}", packetPump.PacketPumpException);
            throw packetPump.PacketPumpException ?? new Exception("Packet pump unexpectedly shut down");
        }

        /// <summary>
        /// Dispatches the packet to the correct handler.
        /// </summary>
        private void HandlePacket(INodePacket packet)
        {
            switch (packet.Type)
            {
                case NodePacketType.ServerNodeConsoleWrite:
                    HandleServerNodeConsoleWrite((ServerNodeConsoleWrite)packet);
                    break;
                case NodePacketType.ServerNodeBuildResult:
                    HandleServerNodeBuildResult((ServerNodeBuildResult)packet);
                    break;
                default: throw new InvalidOperationException($"Unexpected packet type {packet.GetType().Name}");
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
            CommunicationsUtilities.Trace("Build response received: exit code {0}, exit type '{1}'", response.ExitCode, response.ExitType);
            _exitResult.MSBuildClientExitType = MSBuildClientExitType.Success;
            _exitResult.MSBuildAppExitTypeString = response.ExitType;
            _buildFinished = true;
        }

        /// <summary>
        /// Connects to MSBuild server.
        /// </summary>
        /// <returns> Whether the client connected to MSBuild server successfully.</returns>
        private bool TryConnectToServer(int timeout)
        {
            try
            {
                _nodeStream.Connect(timeout);

                int[] handshakeComponents = _handshake.RetrieveHandshakeComponents();
                for (int i = 0; i < handshakeComponents.Length; i++)
                {
                    CommunicationsUtilities.Trace("Writing handshake part {0} ({1}) to pipe {2}", i, handshakeComponents[i], _pipeName);
                    _nodeStream.WriteIntForHandshake(handshakeComponents[i]);
                }

                // This indicates that we have finished all the parts of our handshake; hopefully the endpoint has as well.
                _nodeStream.WriteEndOfHandshakeSignal();

                CommunicationsUtilities.Trace("Reading handshake from pipe {0}", _pipeName);

#if NETCOREAPP2_1_OR_GREATER || MONO
                _nodeStream.ReadEndOfHandshakeSignal(false, 1000); 
#else
                _nodeStream.ReadEndOfHandshakeSignal(false);
#endif

                CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", _pipeName);
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace("Failed to connect to server: {0}", ex);
                _exitResult.MSBuildClientExitType = MSBuildClientExitType.ConnectionError;
                return false;
            }

            return true;
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
