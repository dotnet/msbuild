// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if !NETFRAMEWORK
using System.Collections.Generic;
#endif
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
#if !NETFRAMEWORK
using Microsoft.CodeAnalysis;
using Microsoft.Diagnostics.NETCore.Client;
#endif
using BackendNativeMethods = Microsoft.Build.BackEnd.NativeMethods;

namespace Microsoft.Build.BackEnd
{
    internal sealed class NodeLauncher : INodeLauncher, IBuildComponent
    {
        public static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrowArgumentOutOfRange(type == BuildComponentType.NodeLauncher, nameof(type));
            return new NodeLauncher();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
        }

        public void ShutdownComponent()
        {
        }

        /// <summary>
        /// Creates a new MSBuild process
        /// </summary>
        public Process Start(string msbuildLocation, string[] commandLineArgs, int nodeId)
        {
            // Disable MSBuild server for a child process.
            // In case of starting msbuild server it prevents an infinite recursion. In case of starting msbuild node we also do not want this variable to be set.
            return DisableMSBuildServer(() =>
                    StartInternal(nodeId, msbuildLocation, commandLineArgs));
        }

        /// <summary>
        /// Creates new MSBuild or dotnet process.
        /// </summary>
        private unsafe Process StartInternal(int nodeId, string msbuildLocation, string[] commandLineArgs)
        {
            // Should always have been set already.
            ErrorUtilities.VerifyThrowInternalLength(msbuildLocation, nameof(msbuildLocation));

            if (!FileSystems.Default.FileExists(msbuildLocation))
            {
                throw new BuildAbortedException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CouldNotFindMSBuildExe", msbuildLocation));
            }

            CommunicationsUtilities.Trace("Launching node from {0}", msbuildLocation);

            string? exeName = msbuildLocation;

#if RUNTIME_TYPE_NETCORE
            // Run the child process with the same host as the currently-running process.
            exeName = CurrentHost.GetCurrentHost();
#endif

            bool shouldSetUpDiagnostics = DiagnosticsAreEnabledForCurrentProcess();
            string? transportName = null;
            if (shouldSetUpDiagnostics)
            {
                transportName = CreateTransportName(nodeId); // nodeId is not needed here since we are not reading the events in this method
                CommunicationsUtilities.Trace("Setting up diagnostics transport for child node: {0}", transportName);
            }

            if (!NativeMethodsShared.IsWindows)
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = exeName;
#if !NETFRAMEWORK
                processStartInfo.ArgumentList.AddRange(commandLineArgs);
#else
                processStartInfo.Arguments = string.Join(" ", commandLineArgs);
#endif
                if (!Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout)
                {
                    // Redirect the streams of worker nodes so that this MSBuild.exe's
                    // parent doesn't wait on idle worker nodes to close streams
                    // after the build is complete.
                    processStartInfo.RedirectStandardInput = true;
                    processStartInfo.RedirectStandardOutput = true;
                    processStartInfo.RedirectStandardError = true;
                    processStartInfo.CreateNoWindow = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDNODEWINDOW"));
                }

                processStartInfo.UseShellExecute = false;

                if (shouldSetUpDiagnostics && transportName is not null)
                {
                    AddDiagnosticsTracing(processStartInfo, transportName);
                }

                Process? process;
                try
                {
                    process = Process.Start(processStartInfo);
                }
                catch (Exception ex)
                {
                    CommunicationsUtilities.Trace(
                           "Failed to launch node from {0}. CommandLine: {1}" + Environment.NewLine + "{2}",
                           msbuildLocation,
                           commandLineArgs,
                           ex.ToString());

                    throw new NodeFailedToLaunchException(ex);
                }

                if (process is null)
                {
                    CommunicationsUtilities.Trace(
                           "Failed to launch node from {0}. CommandLine: {1}",
                           msbuildLocation,
                           commandLineArgs);
                    throw new NodeFailedToLaunchException(new Exception("Process.Start returned null"));
                }

#if !NETFRAMEWORK
                if (shouldSetUpDiagnostics)
                {
                    SetUpEventPipeSession(process, transportName!);
                }
#endif
                CommunicationsUtilities.Trace("Successfully launched {1} node with PID {0}", process.Id, exeName);
                return process;
            }
            else
            {
                // Repeat the executable name as the first token of the command line because the command line
                // parser logic expects it and will otherwise skip the first argument
                var spawnArgs = $"\"{msbuildLocation}\" {string.Join(" ", commandLineArgs)}";

                BackendNativeMethods.STARTUP_INFO startInfo = new();
                startInfo.cb = Marshal.SizeOf<BackendNativeMethods.STARTUP_INFO>();

                // Null out the process handles so that the parent process does not wait for the child process
                // to exit before it can exit.
                uint creationFlags = 0;
                if (Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout)
                {
                    creationFlags = BackendNativeMethods.NORMALPRIORITYCLASS;
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

#if RUNTIME_TYPE_NETCORE
                // Repeat the executable name in the args to suit CreateProcess
                spawnArgs = $"\"{exeName}\" {commandLineArgs}";
#endif

                BackendNativeMethods.PROCESS_INFORMATION processInfo = new();
                BackendNativeMethods.SECURITY_ATTRIBUTES processSecurityAttributes = new();
                BackendNativeMethods.SECURITY_ATTRIBUTES threadSecurityAttributes = new();
                processSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();
                threadSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();

                string? envVarWin32String = null;
                if (shouldSetUpDiagnostics && transportName is not null)
                {
                    envVarWin32String = "DOTNET_DiagnosticPorts=" + transportName + '\0';
                }
                bool result = false;
                fixed (char* envBlockPtr = envVarWin32String)
                {
                    result = BackendNativeMethods.CreateProcess(
                            exeName,
                            spawnArgs,
                            ref processSecurityAttributes,
                            ref threadSecurityAttributes,
                            false,
                            creationFlags,
                            lpEnvironment: (nint)envBlockPtr,
                            lpCurrentDirectory: null,
                            ref startInfo,
                            out processInfo);
                }

                if (!result)
                {
                    // Creating an instance of this exception calls GetLastWin32Error and also converts it to a user-friendly string.
                    System.ComponentModel.Win32Exception e = new System.ComponentModel.Win32Exception();

                    CommunicationsUtilities.Trace(
                            "Failed to launch node from {0}. System32 Error code {1}. Description {2}. CommandLine: {2}",
                            msbuildLocation,
                            e.NativeErrorCode.ToString(CultureInfo.InvariantCulture),
                            e.Message,
                            commandLineArgs);

                    throw new NodeFailedToLaunchException(e.NativeErrorCode.ToString(CultureInfo.InvariantCulture), e.Message);
                }

                int childProcessId = processInfo.dwProcessId;

                if (processInfo.hProcess != IntPtr.Zero && processInfo.hProcess != BackendNativeMethods.InvalidHandle)
                {
                    NativeMethodsShared.CloseHandle(processInfo.hProcess);
                }

                if (processInfo.hThread != IntPtr.Zero && processInfo.hThread != BackendNativeMethods.InvalidHandle)
                {
                    NativeMethodsShared.CloseHandle(processInfo.hThread);
                }

                CommunicationsUtilities.Trace("Successfully launched {1} node with PID {0}", childProcessId, exeName);
                return Process.GetProcessById(childProcessId);
            }
        }

        private static string CreateTransportName(int nodeId)
        {
            string transportName = $"msbuild-worker-{nodeId}-for-{Process.GetCurrentProcess().Id}-{DateTime.Now:yyyyMMdd_HHmmss}.socket";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return transportName;
            }
            else
            {
                return Path.Combine(Path.GetTempPath(), transportName);
            }
        }

        private ProcessStartInfo AddDiagnosticsTracing(ProcessStartInfo startInfo, string transportName)
        {
            // Update the environment for the child process to use that pipe name
            startInfo.EnvironmentVariables["DOTNET_DiagnosticPorts"] = transportName;
            return startInfo;
        }

#if !NETFRAMEWORK
        private void SetUpEventPipeSession(Process process, string transportName)
        {
            // * create a list of providers to collect metrics for
            List<EventPipeProvider> providers = [
                new EventPipeProvider("Microsoft.Build", System.Diagnostics.Tracing.EventLevel.Verbose)
            ];

            // * connect a diagnostics session to the child with those providers on our predefined pipe
            var client = new DiagnosticsClient(process.Id);

            // reduce the default buffer size from 256MB because we will have several child nodes
            EventPipeSession session = client.StartEventPipeSession(providers, requestRundown: false, circularBufferMB: 32);

            // * start reading events from that session and forwarding them to our own event source
            // kick the runtime to start the child node
            // now process the events _somehow_
            client.ResumeRuntime();

            // clean up the transport on process exit
            process.Exited += (sender, args) =>
            {
                session.Stop();
                session.Dispose();
                try
                {
                    File.Delete(transportName);
                }
                catch
                {
                }
            };
        }
#endif

        /// <summary>
        /// Checks if the DOTNET_DiagnosticsPorts environment variable is set for the current process
        /// AND if DOTNET_EnableDiagnostics is not set to 0.
        /// </summary>
        private bool DiagnosticsAreEnabledForCurrentProcess() => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_DiagnosticsPorts"))
            && Environment.GetEnvironmentVariable("DOTNET_EnableDiagnostics") is string enableDiagnostics && enableDiagnostics != "0";

        private static Process DisableMSBuildServer(Func<Process> func)
        {
            string? useMSBuildServerEnvVarValue = Environment.GetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName);
            try
            {
                if (useMSBuildServerEnvVarValue is not null)
                {
                    Environment.SetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName, "0");
                }

                return func();
            }
            finally
            {
                if (useMSBuildServerEnvVarValue is not null)
                {
                    Environment.SetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName, useMSBuildServerEnvVarValue);
                }
            }
        }
    }
}
