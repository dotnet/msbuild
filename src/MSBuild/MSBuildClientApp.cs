// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using System.Threading;

#if RUNTIME_TYPE_NETCORE || MONO
using System.IO;
using System.Diagnostics;
#endif

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This class implements client for MSBuild server. It
    /// 1. starts the MSBuild server in a separate process if it does not yet exist.
    /// 2. establishes a connection with MSBuild server and sends a build request.
    /// 3. if server is busy, it falls back to old build behavior.
    /// </summary>
    internal static class MSBuildClientApp
    {
        /// <summary>
        /// This is the entry point for the MSBuild client.
        /// </summary>
        /// <param name="commandLine">The command line to process. The first argument
        /// on the command line is assumed to be the name/path of the executable, and
        /// is ignored.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A value of type <see cref="MSBuildApp.ExitType"/> that indicates whether the build succeeded,
        /// or the manner in which it failed.</returns>
        /// <remarks>
        /// The locations of msbuild exe/dll and dotnet.exe would be automatically detected if called from dotnet or msbuild cli. Calling this function from other executables might not work.
        /// </remarks>
        public static MSBuildApp.ExitType Execute(
#if FEATURE_GET_COMMANDLINE
            string commandLine,
#else
            string[] commandLine,
#endif
            CancellationToken cancellationToken
            )
        {
            string? exeLocation;
            string? dllLocation;

#if RUNTIME_TYPE_NETCORE || MONO
            // Run the child process with the same host as the currently-running process.
            // Mono automagically uses the current mono, to execute a managed assembly.
            if (!NativeMethodsShared.IsMono)
            {
                // _exeFileLocation consists the msbuild dll instead.
                dllLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;;
                exeLocation = GetCurrentHost();
            }
            else
            {
                // _exeFileLocation consists the msbuild dll instead.
                exeLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
                dllLocation = String.Empty;
            }
#else
            exeLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
            dllLocation = String.Empty;
#endif

            return Execute(
                commandLine,
                cancellationToken,
                exeLocation,
                dllLocation
            );
        }

        /// <summary>
        /// This is the entry point for the MSBuild client.
        /// </summary>
        /// <param name="commandLine">The command line to process. The first argument
        /// on the command line is assumed to be the name/path of the executable, and
        /// is ignored.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="exeLocation">Location of executable file to launch the server process.
        /// That should be either dotnet.exe or MSBuild.exe location.</param>
        /// <param name="dllLocation">Location of dll file to launch the server process if needed.
        /// Empty if executable is msbuild.exe and not empty if dotnet.exe.</param>
        /// <returns>A value of type <see cref="MSBuildApp.ExitType"/> that indicates whether the build succeeded,
        /// or the manner in which it failed.</returns>
        public static MSBuildApp.ExitType Execute(
#if FEATURE_GET_COMMANDLINE
            string commandLine,
#else
            string[] commandLine,
#endif
            CancellationToken cancellationToken,
            string exeLocation,
            string dllLocation
        )
        {
            // MSBuild client orchestration.
#if !FEATURE_GET_COMMANDLINE
            string commandLineString = string.Join(" ", commandLine); 
#else
            string commandLineString = commandLine;
#endif
            MSBuildClient msbuildClient = new MSBuildClient(exeLocation, dllLocation); 
            MSBuildClientExitResult exitResult = msbuildClient.Execute(commandLineString, cancellationToken);

            if (exitResult.MSBuildClientExitType == MSBuildClientExitType.ServerBusy
                || exitResult.MSBuildClientExitType == MSBuildClientExitType.ConnectionError
            )
            {
                // Server is busy, fallback to old behavior.
                return MSBuildApp.Execute(commandLine);
            }
            else if ((exitResult.MSBuildClientExitType == MSBuildClientExitType.Success)
                    && Enum.TryParse(exitResult.MSBuildAppExitTypeString, out MSBuildApp.ExitType MSBuildAppExitType))
            {
                // The client successfully set up a build task for MSBuild server and recieved the result.
                // (Which could be a failure as well). Return the recieved exit type. 
                return MSBuildAppExitType;
            }

            return MSBuildApp.ExitType.MSBuildClientFailure;
        }

        // Copied from NodeProviderOutOfProcBase.cs
#if RUNTIME_TYPE_NETCORE || MONO
        private static string? CurrentHost;
        private static string GetCurrentHost()
        {
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
                        CurrentHost = currentProcess.MainModule?.FileName ?? throw new InvalidOperationException("Failed to retrieve process executable.");
                    }
                }
            }

            return CurrentHost;
        }
#endif
    }
}
