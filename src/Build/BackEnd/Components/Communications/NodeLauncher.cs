// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using BackendNativeMethods = Microsoft.Build.BackEnd.NativeMethods;

#nullable disable

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
        public Process Start(string msbuildLocation, string commandLineArgs, int nodeId)
        {
            // Disable MSBuild server for a child process.
            // In case of starting msbuild server it prevents an infinite recurson. In case of starting msbuild node we also do not want this variable to be set.
            return DisableMSBuildServer(() => StartInternal(msbuildLocation, commandLineArgs));
        }

        /// <summary>
        /// Creates a new MSBuild process
        /// </summary>
        private Process StartInternal(string msbuildLocation, string commandLineArgs)
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

            CommunicationsUtilities.Trace("Launching node from {0}", msbuildLocation);

            string exeName = msbuildLocation;

#if RUNTIME_TYPE_NETCORE || MONO
            // Mono automagically uses the current mono, to execute a managed assembly
            if (!NativeMethodsShared.IsMono)
            {
                // Run the child process with the same host as the currently-running process.
                exeName = CurrentHost.GetCurrentHost();
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

                Process process;
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

                CommunicationsUtilities.Trace("Successfully launched {1} node with PID {0}", process.Id, exeName);
                return process;
            }
            else
            {
#if RUNTIME_TYPE_NETCORE
                // Repeat the executable name in the args to suit CreateProcess
                commandLineArgs = $"\"{exeName}\" {commandLineArgs}";
#endif

                BackendNativeMethods.PROCESS_INFORMATION processInfo = new();
                BackendNativeMethods.SECURITY_ATTRIBUTES processSecurityAttributes = new();
                BackendNativeMethods.SECURITY_ATTRIBUTES threadSecurityAttributes = new();
                processSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();
                threadSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();

                bool result = BackendNativeMethods.CreateProcess(
                        exeName,
                        commandLineArgs,
                        ref processSecurityAttributes,
                        ref threadSecurityAttributes,
                        false,
                        creationFlags,
                        BackendNativeMethods.NullPtr,
                        null,
                        ref startInfo,
                        out processInfo);

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

        private static Process DisableMSBuildServer(Func<Process> func)
        {
            string useMSBuildServerEnvVarValue = Environment.GetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName);
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
