// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Represents the configuration data needed to launch a node process.
    /// </summary>
    /// <param name="MSBuildLocation">The path to the executable to launch (e.g., MSBuild.exe or dotnet.exe).</param>
    /// <param name="CommandLineArgs">The command line arguments to pass to the executable.</param>
    /// <param name="EnvironmentOverrides">
    /// Optional environment variable overrides for the process.
    /// A non-null value sets or overrides that variable. A null value removes the variable
    /// from the child process environment - this is used to clear architecture-specific
    /// DOTNET_ROOT variants (e.g., DOTNET_ROOT_X64) that would otherwise take precedence
    /// over DOTNET_ROOT when launching an app host.
    /// </param>
    internal readonly record struct NodeLaunchData(
        string MSBuildLocation,
        string CommandLineArgs,
        IDictionary<string, string?>? EnvironmentOverrides = null)
    {
        internal static NodeLaunchData<WorkerNodeHandshake> ForWorker(
            bool nodeReuse,
            bool lowPriority)
            => new(
                msBuildLocation: null,
                commandLineArgs: $"/noautoresponse /nologo {NodeModeHelper.ToCommandLineArgument(NodeMode.OutOfProcNode)} /nodeReuse:{nodeReuse.ToString().ToLower()} /low:{lowPriority.ToString().ToLower()}",
                handshake: CommunicationsUtilities.GetWorkerNodeHandshake(nodeReuse, lowPriority),
                DotnetHostEnvironmentHelper.CreateDotnetRootEnvironmentOverrides());

        internal static NodeLaunchData<TaskHostHandshake> ForNetTaskHostAppHost(
            NetTaskHostRequest request,
            string msBuildLocation,
            string? toolsDirectory,
            bool lowPriority)
            => new(
                msBuildLocation,
                GetTaskHostCommandLineArgs(request.Options.NodeReuse, lowPriority),
                request.CreateHandshake(toolsDirectory),
                DotnetHostEnvironmentHelper.CreateDotnetRootEnvironmentOverrides(request.LaunchPaths.DotnetHostPath)
                    ?? throw new NodeFailedToLaunchException(errorCode: null, ResourceUtilities.GetResourceString("DotnetHostPathNotSet")));

        internal static NodeLaunchData<TaskHostHandshake> ForNetTaskHostDll(
            NetTaskHostRequest request,
            string msBuildDllPath,
            string? toolsDirectory,
            bool lowPriority)
        {
            string? dotnetHostPath = request.LaunchPaths.DotnetHostPath;
#if RUNTIME_TYPE_NETCORE
            if (string.IsNullOrEmpty(dotnetHostPath))
            {
                dotnetHostPath = CurrentHost.GetCurrentHost();
            }
#endif

            return new(
                dotnetHostPath,
                $"\"{msBuildDllPath}\" {GetTaskHostCommandLineArgs(request.Options.NodeReuse, lowPriority)}",
                request.CreateHandshake(toolsDirectory));
        }

        internal static NodeLaunchData<TaskHostHandshake> ForFrameworkTaskHost(
            FrameworkTaskHostRequest request,
            string msBuildLocation,
            bool lowPriority)
            => new(
                msBuildLocation,
                GetTaskHostCommandLineArgs(request.Options.NodeReuse, lowPriority),
                request.CreateHandshake());

        internal static NodeLaunchData<TaskHostHandshake> ForExternalFrameworkTaskHost(
            FrameworkTaskHostRequest request,
            string msBuildLocation,
            string toolsDirectory,
            Version fileVersion,
            bool lowPriority)
            => new(
                msBuildLocation,
                GetTaskHostCommandLineArgs(request.Options.NodeReuse, lowPriority),
                request.CreateHandshake(toolsDirectory, fileVersion),
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MSBUILD_EXE_PATH"] = msBuildLocation,
                });

        internal static NodeLaunchData<TaskHostHandshake> ForClr2TaskHost(
            Clr2TaskHostRequest request,
            string msBuildLocation,
            string toolsDirectory,
            Version? fileVersion = null)
            => new(
                msBuildLocation,
                string.Empty,
                request.CreateHandshake(toolsDirectory, fileVersion));

        private static string GetTaskHostCommandLineArgs(bool nodeReuse, bool lowPriority)
            => $"/nologo {NodeModeHelper.ToCommandLineArgument(NodeMode.OutOfProcTaskHostNode)} /nodereuse:{nodeReuse} /low:{lowPriority} /parentpacketversion:{NodePacketTypeExtensions.PacketVersion} ";
    }

    internal readonly record struct NodeLaunchData<THandshake>
        where THandshake : ILaunchableNodeHandshake
    {
        internal NodeLaunchData(
            string? msBuildLocation,
            string commandLineArgs,
            THandshake handshake,
            IDictionary<string, string?>? environmentOverrides = null)
        {
            MSBuildLocation = msBuildLocation;
            CommandLineArgs = commandLineArgs;
            Handshake = handshake;
            EnvironmentOverrides = environmentOverrides;
        }

        internal string? MSBuildLocation { get; }
        internal string CommandLineArgs { get; }
        internal THandshake Handshake { get; }
        internal NodeMode? ExpectedNodeMode => Handshake.ExpectedNodeMode;
        internal IDictionary<string, string?>? EnvironmentOverrides { get; }
    }

    internal interface INodeLauncher
    {
        /// <summary>
        /// Creates a new MSBuild process using the specified launch configuration.
        /// </summary>
        /// <param name="launchData">The configuration data for launching the node process.</param>
        /// <param name="nodeId">The unique identifier for the node being launched.</param>
        /// <returns>The started process.</returns>
        Process Start(NodeLaunchData launchData, int nodeId);
    }
}
