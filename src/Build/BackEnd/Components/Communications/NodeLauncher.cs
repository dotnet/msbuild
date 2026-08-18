// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

#if RUNTIME_TYPE_NETCORE
using System.IO;
#endif

using System.Runtime.Versioning;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
#if FEATURE_WINDOWSINTEROP
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Build.Utilities;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
#endif

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

            string exeName = ResolveExecutableName(nodeLaunchData.MSBuildLocation, out bool isNativeAppHost);
            bool ensureStdOut = Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout;
            bool showNodeWindow = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDNODEWINDOW"));
            bool redirectStreams = !ensureStdOut && !showNodeWindow;

            CommunicationsUtilities.Trace($"Launching node from {nodeLaunchData.MSBuildLocation}");

#if FEATURE_WINDOWSINTEROP
            if (NativeMethodsShared.IsWindows)
            {
                return StartProcessWindows(nodeLaunchData, exeName, ensureStdOut, showNodeWindow, redirectStreams, isNativeAppHost);
            }
#endif
            return NativeMethodsShared.IsUnixLike
                ? StartProcessUnix(nodeLaunchData, exeName, redirectStreams, isNativeAppHost)
                : throw new PlatformNotSupportedException();

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
            string fileName = Path.GetFileName(msbuildLocation);

            // Only managed assemblies (.dll) need dotnet.exe as a host.
            // All native executables — MSBuild app host, MSBuildTaskHost.exe, etc. — run directly.
            if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return CurrentHost.GetCurrentHost();
            }

            // Any .exe or extensionless binary (Linux app host) is a native executable.
            isNativeAppHost = true;
#endif
            return msbuildLocation;
        }

        [UnsupportedOSPlatform("windows")]
        private Process StartProcessUnix(NodeLaunchData nodeLaunchData, string exeName, bool redirectStreams, bool isNativeAppHost)
        {
            // Builds command line args for Unix Process.Start, which sets argv[0] from FileName
            // automatically. We must not duplicate the executable name in Arguments for native
            // app hosts. For dotnet-hosted launches, the assembly path must be included so dotnet
            // knows which assembly to run.
            string commandLineArgs = isNativeAppHost ? nodeLaunchData.CommandLineArgs : $"\"{nodeLaunchData.MSBuildLocation}\" {nodeLaunchData.CommandLineArgs}";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = exeName,
                Arguments = commandLineArgs,
                UseShellExecute = false,
                RedirectStandardInput = redirectStreams,
                RedirectStandardOutput = redirectStreams,
                RedirectStandardError = redirectStreams,
                CreateNoWindow = redirectStreams,
            };

            DotnetHostEnvironmentHelper.ApplyEnvironmentOverrides(processStartInfo.Environment, nodeLaunchData.EnvironmentOverrides);

            try
            {
                Process process = Process.Start(processStartInfo);
                CommunicationsUtilities.Trace($"Successfully launched {exeName} node with PID {process.Id}");
                return process;
            }
            catch (Exception ex)
            {
                CommunicationsUtilities.Trace(
                    $"Failed to launch node from {nodeLaunchData.MSBuildLocation}. CommandLine: {commandLineArgs}{Environment.NewLine}{ex}");

                throw new NodeFailedToLaunchException(ex);
            }
        }

#if FEATURE_WINDOWSINTEROP
        [SupportedOSPlatform("windows6.1")]
        private static unsafe Process StartProcessWindows(NodeLaunchData nodeLaunchData, string exeName, bool ensureStdOut, bool showNodeWindow, bool redirectStreams, bool isNativeAppHost)
        {
            PROCESS_CREATION_FLAGS creationFlags = (ensureStdOut, showNodeWindow) switch
            {
                (true, true) => PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS | PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE,
                (true, false) => PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS,
                (false, true) => PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE,
                (false, false) => PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW,
            };

            STARTUPINFOW startInfo = CreateStartupInfo(redirectStreams);

            // CreateProcessW requires a writable PWSTR for lpCommandLine. Build it into a ValueStringBuilder
            // we can pin directly. The MSBuild path is repeated as the first token of the command line because
            // the parser logic expects it and will otherwise skip the first argument; on .NET Core the dotnet host
            // path is also prepended for managed assembly launches.
            ValueStringBuilder commandLine = new(stackalloc char[256]);
            ValueStringBuilder environmentBlock = new(stackalloc char[512]);
            try
            {
#if RUNTIME_TYPE_NETCORE
                if (!isNativeAppHost)
                {
                    commandLine.Append('"');
                    commandLine.Append(exeName);
                    commandLine.Append("\" ");
                }
#endif
                commandLine.Append('"');
                commandLine.Append(nodeLaunchData.MSBuildLocation);
                commandLine.Append("\" ");
                commandLine.Append(nodeLaunchData.CommandLineArgs);

                bool hasEnvironmentBlock = BuildEnvironmentBlock(ref environmentBlock, nodeLaunchData.EnvironmentOverrides);

                // When passing a Unicode environment block, we must set CREATE_UNICODE_ENVIRONMENT.
                // Without this flag, CreateProcess interprets the block as ANSI, causing error 87.
                if (hasEnvironmentBlock)
                {
                    creationFlags |= PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;
                }

                PROCESS_INFORMATION processInfo;
                BOOL result;
                fixed (char* pCommandLine = commandLine)
                {
                    fixed (char* pExeName = exeName)
                    {
                        fixed (char* pEnvironmentBlock = environmentBlock)
                        {
                            // Note: CreateProcess is documented to be allowed to modify lpCommandLine in-place
                            // (it may insert a null terminator to split the exe from the args). The buffer must
                            // not be read after a successful call. We only read commandLine again on failure
                            // (for tracing), where in practice the OS does not mutate the buffer.
                            result = PInvoke.CreateProcess(
                                lpApplicationName: pExeName,
                                lpCommandLine: pCommandLine,
                                lpProcessAttributes: null,
                                lpThreadAttributes: null,
                                bInheritHandles: false,
                                dwCreationFlags: creationFlags,
                                lpEnvironment: hasEnvironmentBlock ? pEnvironmentBlock : null,
                                lpCurrentDirectory: (PCWSTR)null,
                                lpStartupInfo: &startInfo,
                                lpProcessInformation: &processInfo);
                        }
                    }
                }

                if (!result)
                {
                    var e = new System.ComponentModel.Win32Exception();

                    string commandLineForTrace = commandLine.ToString();
                    CommunicationsUtilities.Trace(
                        $"Failed to launch node from {nodeLaunchData.MSBuildLocation}. System32 Error code {e.NativeErrorCode.ToString(CultureInfo.InvariantCulture)}. Description {e.Message}. CommandLine: {commandLineForTrace}");

                    throw new NodeFailedToLaunchException(e.NativeErrorCode.ToString(CultureInfo.InvariantCulture), e.Message);
                }

                CloseProcessHandles(processInfo);

                CommunicationsUtilities.Trace($"Successfully launched {exeName} node with PID {(int)processInfo.dwProcessId}");
                return Process.GetProcessById((int)processInfo.dwProcessId);
            }
            finally
            {
                commandLine.Dispose();
                environmentBlock.Dispose();
            }

            static void CloseProcessHandles(PROCESS_INFORMATION processInfo)
            {
#pragma warning disable CA1416 // static local functions don't inherit [SupportedOSPlatform] (analyzer limitation)
                if (processInfo.hProcess != HANDLE.Null && processInfo.hProcess != HANDLE.INVALID_HANDLE_VALUE)
                {
                    PInvoke.CloseHandle(processInfo.hProcess);
                }

                if (processInfo.hThread != HANDLE.Null && processInfo.hThread != HANDLE.INVALID_HANDLE_VALUE)
                {
                    PInvoke.CloseHandle(processInfo.hThread);
                }
#pragma warning restore CA1416
            }
        }
#endif

#if FEATURE_WINDOWSINTEROP
        [SupportedOSPlatform("windows6.1")]
        private static STARTUPINFOW CreateStartupInfo(bool redirectStreams)
        {
            var startInfo = new STARTUPINFOW
            {
                cb = (uint)Marshal.SizeOf<STARTUPINFOW>(),
            };

            if (redirectStreams)
            {
                startInfo.hStdError = HANDLE.INVALID_HANDLE_VALUE;
                startInfo.hStdInput = HANDLE.INVALID_HANDLE_VALUE;
                startInfo.hStdOutput = HANDLE.INVALID_HANDLE_VALUE;
                startInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES;
            }

            return startInfo;
        }

        /// <summary>
        /// Builds a Windows environment block for CreateProcess into the supplied builder.
        /// </summary>
        /// <param name="builder">Builder that receives the null-separated, double-null-terminated environment block.</param>
        /// <param name="environmentOverrides">Environment variable overrides. Null values remove variables.</param>
        /// <returns>
        /// <see langword="true"/> if a block was written; <see langword="false"/> when no overrides were supplied
        /// (caller passes the inherited environment).
        /// </returns>
        [SupportedOSPlatform("windows")]
        private static bool BuildEnvironmentBlock(ref ValueStringBuilder builder, IDictionary<string, string> environmentOverrides)
        {
            if (environmentOverrides == null || environmentOverrides.Count == 0)
            {
                return false;
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

            foreach (string key in sortedKeys)
            {
                builder.Append(key);
                builder.Append('=');
                builder.Append(environment[key]);
                builder.Append('\0');
            }

            builder.Append('\0');

            return true;
        }
#endif

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
