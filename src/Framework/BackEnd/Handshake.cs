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
    /// For the .NET task host on Windows, the salt computed from the tools directory with the
    /// opposite drive-letter casing (e.g. "d:\..." in addition to "D:\..."), or <see langword="null"/>
    /// when not applicable. The parent and child derive the tools directory from different sources
    /// (the parent from the <c>$(NetCoreSdkRoot)</c> property, the child from its own process path)
    /// whose only realistic divergence is drive-letter casing; both spellings denote the same
    /// directory. The child accepts this variant so it can connect to a parent that spelled the
    /// drive differently. This only widens what the child accepts, so it never rejects a parent that
    /// connects successfully today.
    /// </summary>
    private readonly int? _netTaskHostSaltDriveCaseVariant;

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
        Assumed.True(
            toolsDirectory is null || IsNetTaskHost || IsClr2TaskHost,
            $"{toolsDirectory} should only be provided for .NET or CLR2 TaskHost nodes.");
#else
        // IsNetTaskHost covers the case when NET process spawns NET TaskHost.
        Assumed.True(
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

        // The .NET task host parent (.NET Framework MSBuild, e.g. Visual Studio) and child
        // (.NET SDK MSBuild) compute this salt independently from different sources, and on Windows
        // those can differ only by drive-letter casing ("D:\..." vs "d:\..."). Because the salt is a
        // case-sensitive hash, that produces a mismatch and the otherwise-valid handshake fails with
        // MSB4216. Precompute the salt for the alternate drive-letter casing so the child also accepts
        // the parent's spelling (see IsNetTaskHostSaltMatch). Only the child consults this value.
        if (IsNetTaskHost && TryGetDriveLetterCaseVariant(toolsDirectory, out string? alternateToolsDirectory))
        {
            _netTaskHostSaltDriveCaseVariant = CommunicationsUtilities.GetHashCode($"{handshakeSalt}{alternateToolsDirectory}");
        }

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

    /// <summary>
    /// Determines whether <paramref name="receivedSalt"/> matches this node's salt computed for the
    /// alternate drive-letter casing of its tools directory. Used only by the .NET task host child to
    /// tolerate a parent that resolved the same SDK directory with different drive-letter casing
    /// (e.g. "D:\..." vs "d:\..."), which would otherwise fail the case-sensitive handshake with
    /// MSB4216. Returns <see langword="false"/> for every other node type and configuration, so the
    /// exact salt remains required there.
    /// </summary>
    public bool IsNetTaskHostSaltMatch(int receivedSalt)
        => _netTaskHostSaltDriveCaseVariant is int alternateSalt
            && receivedSalt == CommunicationsUtilities.AvoidEndOfHandshakeSignal(alternateSalt);

    /// <summary>
    /// Produces <paramref name="path"/> with the drive letter switched to the opposite casing
    /// (e.g. "D:\dir" -&gt; "d:\dir"). Returns <see langword="false"/> when there is nothing to flip:
    /// off Windows, for empty paths, for paths without a drive letter (such as UNC paths), or when the
    /// drive letter is not an ASCII letter.
    /// </summary>
    private static bool TryGetDriveLetterCaseVariant(string? path, out string? variant)
    {
        variant = null;

        if (!NativeMethods.IsWindows || string.IsNullOrEmpty(path) || path!.Length < 2 || path[1] != ':')
        {
            return false;
        }

        char drive = path[0];
        char flippedDrive = char.IsUpper(drive)
            ? char.ToLowerInvariant(drive)
            : char.IsLower(drive) ? char.ToUpperInvariant(drive) : drive;

        if (flippedDrive == drive)
        {
            return false;
        }

        variant = flippedDrive + path.Substring(1);
        return true;
    }

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
        // Read the file version from the assembly's AssemblyFileVersionAttribute rather than from the file on
        // disk: Assembly.Location is empty in a single-file/Native AOT host (and the on-disk read carries an
        // IL3000), while the attribute carries the same value and is preserved under trimming.
        var fileVersion = new Version(
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);

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
