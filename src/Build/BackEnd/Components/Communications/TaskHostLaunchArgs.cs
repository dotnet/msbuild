// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

#if NET
#else
using Microsoft.IO;
#endif

namespace Microsoft.Build.BackEnd;

internal sealed class TaskHostLaunchArgs
{
    public string ExePath { get; }

    public string CommandLineArgs { get; }

    public Handshake Handshake { get; }

    public bool UsingDotNetExe { get; }

    private TaskHostLaunchArgs(
        string exePath,
        string commandLineArgs,
        Handshake handshake,
        bool usingDotNetExe = false)
    {
        ExePath = exePath;
        CommandLineArgs = commandLineArgs;
        Handshake = handshake;
        UsingDotNetExe = usingDotNetExe;
    }

    public static bool TryCreate(
        ref readonly TaskHostParameters taskHostParameters,
        BuildParameters buildParameters,
        HandshakeOptions hostContext,
        [NotNullWhen(true)] out TaskHostLaunchArgs? result)
    {
        string msbuildLocation;
        string commandLineArgs;
        Handshake handshake;
        bool nodeReuse;

#if NETFRAMEWORK

        // Handle scenario where a .NET task host is launched from .NET Framework
        if (Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.NET))
        {
            (string runtimeHostPath, string msbuildAssemblyDirectory) = NodeProviderOutOfProcTaskHost.GetMSBuildLocationForNETRuntime(hostContext, taskHostParameters);

            msbuildLocation = Path.Combine(msbuildAssemblyDirectory, Constants.MSBuildAssemblyName);
            nodeReuse = Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.NodeReuse);

            commandLineArgs = $"""
                "{msbuildLocation}" /nologo {NodeModeHelper.ToCommandLineArgument(NodeMode.OutOfProcTaskHostNode)} /nodereuse:{nodeReuse.ToString().ToLower()} /low:{buildParameters.LowPriority.ToString().ToLower()} /parentpacketversion:{NodePacketTypeExtensions.PacketVersion} 
                """;

            handshake = new Handshake(hostContext, toolsDirectory: msbuildAssemblyDirectory);

            result = new TaskHostLaunchArgs(runtimeHostPath, commandLineArgs, handshake, usingDotNetExe: true);
            return true;
        }
#endif

        msbuildLocation = NodeProviderOutOfProcTaskHost.GetMSBuildExecutablePathForNonNETRuntimes(hostContext);

        // we couldn't even figure out the location we're trying to launch ... just go ahead and fail.
        if (msbuildLocation == null)
        {
            result = null;
            return false;
        }

#if FEATURE_NET35_TASKHOST
        if (Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.CLR2))
        {
            // The .NET 3.5 task host uses the directory of its EXE when calculating salt for the handshake.
            string toolsDirectory = Path.GetDirectoryName(msbuildLocation) ?? string.Empty;

            // MSBuildTaskHost doesn't use command-line arguments. 
            commandLineArgs = "";
            handshake = new Handshake(hostContext, toolsDirectory);

            result = new TaskHostLaunchArgs(msbuildLocation, commandLineArgs, handshake);
            return true;
        }
#endif

        nodeReuse = Handshake.IsHandshakeOptionEnabled(hostContext, HandshakeOptions.NodeReuse);

        commandLineArgs = $"""
            /nologo {NodeModeHelper.ToCommandLineArgument(NodeMode.OutOfProcTaskHostNode)} /nodereuse:{nodeReuse.ToString().ToLower()} /low:{buildParameters.LowPriority.ToString().ToLower()} /parentpacketversion:{NodePacketTypeExtensions.PacketVersion} 
            """;

        handshake = new Handshake(hostContext);

        result = new TaskHostLaunchArgs(msbuildLocation, commandLineArgs, handshake);
        return true;
    }
}
