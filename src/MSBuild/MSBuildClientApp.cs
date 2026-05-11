// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Threading;
using Microsoft.Build.Experimental;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.Shared;

#if RUNTIME_TYPE_NETCORE
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
        public static MSBuildApp.ExitType Execute(string[] commandLineArgs, CancellationToken cancellationToken)
        {
            string msbuildLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;

            return Execute(
                commandLineArgs,
                msbuildLocation,
                cancellationToken);
        }

        /// <summary>
        /// This is the entry point for the MSBuild client.
        /// </summary>
        /// <param name="commandLineArgs">The command line to process. The first argument
        /// on the command line is assumed to be the name/path of the executable, and
        /// is ignored.</param>
        /// <param name="msbuildLocation"> Full path to current MSBuild.exe if executable is MSBuild.exe,
        /// or to version of MSBuild.dll found to be associated with the current process.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A value of type <see cref="MSBuildApp.ExitType"/> that indicates whether the build succeeded,
        /// or the manner in which it failed.</returns>
        public static MSBuildApp.ExitType Execute(string[] commandLineArgs, string msbuildLocation, CancellationToken cancellationToken)
        {
            MSBuildClient msbuildClient = new MSBuildClient(commandLineArgs, msbuildLocation);
            MSBuildClientExitResult exitResult = msbuildClient.Execute(cancellationToken);

            if (exitResult.MSBuildClientExitType == MSBuildClientExitType.ServerBusy ||
                exitResult.MSBuildClientExitType == MSBuildClientExitType.UnableToConnect ||
                exitResult.MSBuildClientExitType == MSBuildClientExitType.UnknownServerState ||
                exitResult.MSBuildClientExitType == MSBuildClientExitType.LaunchError)
            {
                if (KnownTelemetry.PartialBuildTelemetry != null)
                {
                    KnownTelemetry.PartialBuildTelemetry.ServerFallbackReason = exitResult.MSBuildClientExitType.ToString();
                }

                // Surface a single user-visible message on stderr when the failure is something
                // other than the well-understood "another client is racing us for the launch
                // mutex" case. Without this the user sees no indication that MSBuild Server was
                // requested but unavailable; previously a connection timeout would even crash
                // the process (the DOTNET_CLI_USE_MSBUILD_SERVER=true regression in 10.0.300).
                if (exitResult.MSBuildClientExitType != MSBuildClientExitType.ServerBusy)
                {
                    string detail = GetServerFallbackDetail(exitResult);
                    Console.Error.WriteLine(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("MSBuildServerUnavailable", detail));
                }

                // Server is busy / unavailable, fallback to old behavior.
                return MSBuildApp.Execute(commandLineArgs);
            }

            if (exitResult.MSBuildClientExitType == MSBuildClientExitType.Success &&
                Enum.TryParse(exitResult.MSBuildAppExitTypeString, out MSBuildApp.ExitType MSBuildAppExitType))
            {
                // The client successfully set up a build task for MSBuild server and received the result.
                // (Which could be a failure as well). Return the received exit type.
                return MSBuildAppExitType;
            }

            return MSBuildApp.ExitType.MSBuildClientFailure;
        }

        /// <summary>
        /// Picks the most specific localized "why MSBuild server was unavailable" sub-message for
        /// the user-visible fallback notice. Prefers the "server crashed immediately on launch"
        /// detail over a generic connect-failure message when the launched server's exit code is
        /// known.
        /// </summary>
        private static string GetServerFallbackDetail(MSBuildClientExitResult exitResult)
        {
            return exitResult.MSBuildClientExitType switch
            {
                MSBuildClientExitType.LaunchError =>
                    ResourceUtilities.FormatResourceStringStripCodeAndKeyword("MSBuildServerLaunchError"),
                MSBuildClientExitType.UnknownServerState =>
                    ResourceUtilities.FormatResourceStringStripCodeAndKeyword("MSBuildServerStateUnknown"),
                MSBuildClientExitType.UnableToConnect when exitResult.ServerProcessExitCode is int code =>
                    ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
                        "MSBuildServerCrashedOnLaunch",
                        code.ToString(CultureInfo.InvariantCulture)),
                // Default: UnableToConnect without a known exit code, or any future MSBuildClientExitType
                // value the caller forwards here. Wording is deliberately neutral about whether the
                // underlying failure was a timeout or a non-timeout connect error.
                _ => ResourceUtilities.FormatResourceStringStripCodeAndKeyword("MSBuildServerConnectFailed"),
            };
        }
    }
}
