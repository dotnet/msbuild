// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

#if RUNTIME_TYPE_NETCORE
using System.IO;
#endif

using System.Runtime.InteropServices;
using System.Text;
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
        /// Creates a new MSBuild process using the specified launch configuration.
        /// </summary>
        public Process Start(NodeLaunchData launchData, int nodeId)
        {
            // Disable MSBuild server for a child process.
            // In case of starting msbuild server it prevents an infinite recursion. In case of starting msbuild node we also do not want this variable to be set.
            return DisableMSBuildServer(() => StartInternal(launchData));
        }

        /// <summary>
        /// Creates new MSBuild or dotnet process.
        /// </summary>
        private Process StartInternal(NodeLaunchData nodeLaunchData)
        {
            ValidateMSBuildLocation(nodeLaunchData.MSBuildLocation);

            // Repeat the executable name as the first token of the command line because the command line
            // parser logic expects it and will otherwise skip the first argument
            string commandLineArgs = $"\"{nodeLaunchData.MSBuildLocation}\" {nodeLaunchData.CommandLineArgs}";
            string exeName = ResolveExecutableName(nodeLaunchData.MSBuildLocation, out bool isNativeAppHost);
            uint creationFlags = GetCreationFlags(out bool redirectStreams);

            CommunicationsUtilities.Trace("Launching node from {0}", nodeLaunchData.MSBuildLocation);

            return NativeMethodsShared.IsWindows
                ? StartProcessWindows(nodeLaunchData, exeName, commandLineArgs, creationFlags, redirectStreams, isNativeAppHost)
                : StartProcessUnix(nodeLaunchData, exeName, commandLineArgs, creationFlags, redirectStreams);

            static void ValidateMSBuildLocation(string msbuildLocation)
            {
                // Should always have been set already.
                ErrorUtilities.VerifyThrowInternalLength(msbuildLocation, nameof(msbuildLocation));

                if (!FileSystems.Default.FileExists(msbuildLocation))
                {
                    throw new BuildAbortedException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CouldNotFindMSBuildExe", msbuildLocation));
                }
            }
        }

        private string ResolveExecutableName(string msbuildLocation, out bool isNativeAppHost)
        {
            isNativeAppHost = false;

#if RUNTIME_TYPE_NETCORE
            // If msbuildLocation is a native app host (e.g., MSBuild.exe on Windows, MSBuild on Linux), run it directly.
            // Otherwise, use dotnet.exe to run the managed assembly (e.g., MSBuild.dll).
            string fileName = Path.GetFileName(msbuildLocation);
            isNativeAppHost = fileName.Equals(Constants.MSBuildExecutableName, StringComparison.OrdinalIgnoreCase);
            if (!isNativeAppHost)
            {
                return CurrentHost.GetCurrentHost();
            }
#endif
            return msbuildLocation;
        }

        private uint GetCreationFlags(out bool redirectStreams)
        {
            bool ensureStdOut = Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout;
            bool showNodeWindow = !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDNODEWINDOW"));

            redirectStreams = !ensureStdOut && !showNodeWindow;

            uint flags = (ensureStdOut, showNodeWindow) switch
            {
                (true, true) => BackendNativeMethods.NORMALPRIORITYCLASS | BackendNativeMethods.CREATE_NEW_CONSOLE,
                (true, false) => BackendNativeMethods.NORMALPRIORITYCLASS,
                (false, true) => BackendNativeMethods.CREATE_NEW_CONSOLE,
                (false, false) => BackendNativeMethods.CREATENOWINDOW,
            };

            return flags;
        }

        private Process StartProcessUnix(NodeLaunchData nodeLaunchData, string exeName, string commandLineArgs, uint creationFlags, bool redirectStreams)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = exeName,
                Arguments = commandLineArgs,
                UseShellExecute = false,
                RedirectStandardInput = redirectStreams,
                RedirectStandardOutput = redirectStreams,
                RedirectStandardError = redirectStreams,
                CreateNoWindow = redirectStreams && (creationFlags & BackendNativeMethods.CREATENOWINDOW) != 0,
            };

            DotnetHostEnvironmentHelper.ApplyEnvironmentOverrides(processStartInfo.Environment, nodeLaunchData.EnvironmentOverrides);

            try
            {
                Process process = Process.Start(processStartInfo);
                CommunicationsUtilities.Trace("Successfully launched {1} node with PID {0}", process.Id, exeName);
                return process;
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace(
                    "Failed to launch node from {0}. CommandLine: {1}" + Environment.NewLine + "{2}",
                    nodeLaunchData.MSBuildLocation,
                    commandLineArgs,
                    ex.ToString());

                throw new NodeFailedToLaunchException(ex);
            }
        }

        private static Process StartProcessWindows(NodeLaunchData nodeLaunchData, string exeName, string commandLineArgs, uint creationFlags, bool redirectStreams, bool isNativeAppHost)
        {
#if RUNTIME_TYPE_NETCORE
            if (!isNativeAppHost)
            {
                commandLineArgs = $"\"{exeName}\" {commandLineArgs}";
            }
#endif

            BackendNativeMethods.STARTUP_INFO startInfo = CreateStartupInfo(redirectStreams);
            BackendNativeMethods.SECURITY_ATTRIBUTES processSecurityAttributes = new() { nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>() };
            BackendNativeMethods.SECURITY_ATTRIBUTES threadSecurityAttributes = new() { nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>() };

            IntPtr environmentBlock = BuildEnvironmentBlock(nodeLaunchData.EnvironmentOverrides);

            // When passing a Unicode environment block, we must set CREATE_UNICODE_ENVIRONMENT.
            // Without this flag, CreateProcess interprets the block as ANSI, causing error 87.
            uint effectiveCreationFlags = creationFlags;
            if (environmentBlock != BackendNativeMethods.NullPtr)
            {
                effectiveCreationFlags |= BackendNativeMethods.CREATE_UNICODE_ENVIRONMENT;
            }

            try
            {
                bool result = BackendNativeMethods.CreateProcess(
                    exeName,
                    commandLineArgs,
                    ref processSecurityAttributes,
                    ref threadSecurityAttributes,
                    false,
                    effectiveCreationFlags,
                    environmentBlock,
                    null,
                    ref startInfo,
                    out BackendNativeMethods.PROCESS_INFORMATION processInfo);

                if (!result)
                {
                    var e = new System.ComponentModel.Win32Exception();

                    CommunicationsUtilities.Trace(
                        "Failed to launch node from {0}. System32 Error code {1}. Description {2}. CommandLine: {3}",
                        nodeLaunchData.MSBuildLocation,
                        e.NativeErrorCode.ToString(CultureInfo.InvariantCulture),
                        e.Message,
                        commandLineArgs);

                    throw new NodeFailedToLaunchException(e.NativeErrorCode.ToString(CultureInfo.InvariantCulture), e.Message);
                }

                CloseProcessHandles(processInfo);

                CommunicationsUtilities.Trace("Successfully launched {1} node with PID {0}", processInfo.dwProcessId, exeName);
                return Process.GetProcessById(processInfo.dwProcessId);
            }
            finally
            {
                if (environmentBlock != BackendNativeMethods.NullPtr)
                {
                    Marshal.FreeHGlobal(environmentBlock);
                }
            }

            static void CloseProcessHandles(BackendNativeMethods.PROCESS_INFORMATION processInfo)
            {
#if WINDOWS
                if (processInfo.hProcess != IntPtr.Zero && processInfo.hProcess != NativeMethods.InvalidHandle)
                {
                    NativeMethodsShared.CloseHandle(processInfo.hProcess);
                }

                if (processInfo.hThread != IntPtr.Zero && processInfo.hThread != NativeMethods.InvalidHandle)
                {
                    NativeMethodsShared.CloseHandle(processInfo.hThread);
                }
#endif
            }
        }

        private static BackendNativeMethods.STARTUP_INFO CreateStartupInfo(bool redirectStreams)
        {
            var startInfo = new BackendNativeMethods.STARTUP_INFO
            {
                cb = Marshal.SizeOf<BackendNativeMethods.STARTUP_INFO>(),
            };

            if (redirectStreams)
            {
                startInfo.hStdError = BackendNativeMethods.InvalidHandle;
                startInfo.hStdInput = BackendNativeMethods.InvalidHandle;
                startInfo.hStdOutput = BackendNativeMethods.InvalidHandle;
                startInfo.dwFlags = BackendNativeMethods.STARTFUSESTDHANDLES;
            }

            return startInfo;
        }

        /// <summary>
        /// Builds a Windows environment block for CreateProcess.
        /// </summary>
        /// <param name="environmentOverrides">Environment variable overrides. Null values remove variables.</param>
        /// <returns>Pointer to environment block that must be freed with Marshal.FreeHGlobal, or BackendNativeMethods.NullPtr.</returns>
        private static IntPtr BuildEnvironmentBlock(IDictionary<string, string> environmentOverrides)
        {
            if (environmentOverrides == null || environmentOverrides.Count == 0)
            {
                return BackendNativeMethods.NullPtr;
            }

            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                environment[(string)entry.Key] = (string)entry.Value;
            }

            DotnetHostEnvironmentHelper.ApplyEnvironmentOverrides(environment, environmentOverrides);

            // Build the environment block: "key=value\0key=value\0\0"
            // Windows CreateProcess requires the environment block to be sorted alphabetically by name (case-insensitive).
            var sortedKeys = new List<string>(environment.Keys);
            sortedKeys.Sort(StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            foreach (string key in sortedKeys)
            {
                sb.Append(key);
                sb.Append('=');
                sb.Append(environment[key]);
                sb.Append('\0');
            }

            sb.Append('\0');

            return Marshal.StringToHGlobalUni(sb.ToString());
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
