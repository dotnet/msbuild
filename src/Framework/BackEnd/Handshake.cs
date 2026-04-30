// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Internal;

internal class Handshake
{
    /// <summary>
    /// Marker indicating that the next integer in the child handshake response is the PacketVersion.
    /// </summary>
    public const int PacketVersionFromChildMarker = -1;

    // The number is selected as an arbitrary value that is unlikely to conflict with any future sdk version.
    public const int NetTaskHostHandshakeVersion = 99;

    protected readonly HandshakeComponents _handshakeComponents;

    /// <summary>
    ///  Initializes a new instance of the <see cref="Handshake"/> class with the specified node type.
    /// </summary>
    /// <param name="nodeType">
    ///  The <see cref="HandshakeOptions"/> that specifies the type of node and configuration options for the handshake operation.
    /// </param>
    public Handshake(HandshakeOptions nodeType)
        : this(nodeType, includeSessionId: true, toolsDirectory: null)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="Handshake"/> class with the specified node type
    ///  and optional predefined tools directory.
    /// </summary>
    /// <param name="nodeType">
    ///  The <see cref="HandshakeOptions"/> that specifies the type of node and configuration options for the handshake operation.
    /// </param>
    /// <param name="toolsDirectory">
    ///  The directory path to use for handshake salt calculation. For some task hosts, notably the .NET TaskHost (on .NET Framework)
    ///  and the CLR2 TaskHost, this is needed to ensure the child process connects with the expected tools directory context.
    /// </param>
    public Handshake(HandshakeOptions nodeType, string toolsDirectory)
        : this(nodeType, includeSessionId: true, toolsDirectory)
    {
    }

    // Helper method to validate handshake option presence
    internal static bool IsHandshakeOptionEnabled(HandshakeOptions hostContext, HandshakeOptions option)
        => (hostContext & option) == option;

    // Source options of the handshake.
    internal HandshakeOptions HandshakeOptions { get; }

    protected Handshake(HandshakeOptions nodeType, bool includeSessionId, string? toolsDirectory)
    {
        HandshakeOptions = nodeType;

#if NETFRAMEWORK
        FrameworkErrorUtilities.VerifyThrow(
            toolsDirectory is null || IsNetTaskHost || IsClr2TaskHost,
            $"{toolsDirectory} should only be provided for .NET or CLR2 TaskHost nodes.");
#else
        // IsNetTaskHost covers the case when NET process spawns NET TaskHost.
        FrameworkErrorUtilities.VerifyThrow(
            toolsDirectory is null || IsNetTaskHost,
            $"{toolsDirectory} should not have been provided.");
#endif

        toolsDirectory ??= BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryRoot;

        // Build handshake options with version in upper bits
        const int handshakeVersion = (int)CommunicationsUtilities.handshakeVersion;
        var options = (int)nodeType | (handshakeVersion << 24);
        CommunicationsUtilities.Trace($"Building handshake for node type {nodeType}, (version {handshakeVersion}): options {options}.");

        // Calculate salt from environment and tools directory
        string handshakeSalt = Environment.GetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT") ?? "";

        int salt = CommunicationsUtilities.GetHashCode($"{handshakeSalt}{toolsDirectory}");

        CommunicationsUtilities.Trace($"Handshake salt is {handshakeSalt}");
        CommunicationsUtilities.Trace($"Tools directory root is {toolsDirectory}");

        // Get session ID if needed (expensive call)
        int sessionId = 0;
        if (includeSessionId && NativeMethods.IsWindows)
        {
            // On Windows, SessionId differentiates RDP sessions.
            // On Unix, getsid() returns the session leader PID which differs per terminal,
            // preventing cross-terminal node reuse. Use 0 since Unix doesn't need
            // RDP-style session isolation.
            using var currentProcess = Process.GetCurrentProcess();
            sessionId = currentProcess.SessionId;
        }

        _handshakeComponents = IsNetTaskHost
            ? CreateNetTaskHostComponents(options, salt, sessionId)
            : CreateStandardComponents(options, salt, sessionId);
    }

    private bool IsNetTaskHost
        => IsHandshakeOptionEnabled(HandshakeOptions, HandshakeOptions.NET | HandshakeOptions.TaskHost);

#if NETFRAMEWORK
    private bool IsClr2TaskHost
        => IsHandshakeOptionEnabled(HandshakeOptions, HandshakeOptions.CLR2 | HandshakeOptions.TaskHost);
#endif

    private static HandshakeComponents CreateNetTaskHostComponents(int options, int salt, int sessionId) => new(
        options,
        salt,
        NetTaskHostHandshakeVersion,
        NetTaskHostHandshakeVersion,
        NetTaskHostHandshakeVersion,
        NetTaskHostHandshakeVersion,
        sessionId);

    private static HandshakeComponents CreateStandardComponents(int options, int salt, int sessionId)
    {
        var fileVersion = new Version(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion!);

        return new(
            options,
            salt,
            fileVersion.Major,
            fileVersion.Minor,
            fileVersion.Build,
            fileVersion.Revision,
            sessionId);
    }

    public virtual HandshakeComponents RetrieveHandshakeComponents() => new(
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.Options),
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.Salt),
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionMajor),
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionMinor),
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionBuild),
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionPrivate),
        CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.SessionId));

    public virtual string GetKey()
        => $"{_handshakeComponents.Options} {_handshakeComponents.Salt} {_handshakeComponents.FileVersionMajor} {_handshakeComponents.FileVersionMinor} {_handshakeComponents.FileVersionBuild} {_handshakeComponents.FileVersionPrivate} {_handshakeComponents.SessionId}";

    public virtual byte? ExpectedVersionInFirstByte
        => CommunicationsUtilities.handshakeVersion;
}
