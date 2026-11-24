// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Internal;

internal interface INodeHandshake
{
    HandshakeOptions HandshakeOptions { get; }
    bool NodeReuse { get; }
    HandshakeComponents RetrieveHandshakeComponents();
    string GetKey();
    byte? ExpectedVersionInFirstByte { get; }
    bool IsNetTaskHostSaltMatch(int receivedSalt);
}

/// <summary>
/// A handshake for a node that MSBuild launches itself, which determines the node mode the launched process runs in.
/// </summary>
internal interface ILaunchableNodeHandshake : INodeHandshake
{
    /// <summary>
    /// The node mode the launched process reports, or <see langword="null"/> when the process does not report one.
    /// </summary>
    NodeMode? ExpectedNodeMode { get; }
}

/// <summary>
/// The closed set of handshake protocols. Node APIs use a concrete case, not this union.
/// </summary>
internal union Handshake(
    WorkerNodeHandshake,
    ServerNodeHandshake,
    NetTaskHostHandshake,
    FrameworkTaskHostHandshake,
    Clr2TaskHostHandshake) : INodeHandshake
{
    public const int PacketVersionFromChildMarker = -1;
    public const int NetTaskHostHandshakeVersion = 99;

    private INodeHandshake Protocol => this switch
    {
        WorkerNodeHandshake handshake => handshake,
        ServerNodeHandshake handshake => handshake,
        NetTaskHostHandshake handshake => handshake,
        FrameworkTaskHostHandshake handshake => handshake,
        Clr2TaskHostHandshake handshake => handshake,
    };

    public HandshakeOptions HandshakeOptions => Protocol.HandshakeOptions;
    public bool NodeReuse => Protocol.NodeReuse;
    public HandshakeComponents RetrieveHandshakeComponents() => Protocol.RetrieveHandshakeComponents();
    public string GetKey() => Protocol.GetKey();
    public byte? ExpectedVersionInFirstByte => Protocol.ExpectedVersionInFirstByte;
    public bool IsNetTaskHostSaltMatch(int receivedSalt) => Protocol.IsNetTaskHostSaltMatch(receivedSalt);

    internal static Handshake From(INodeHandshake handshake) => handshake switch
    {
        Handshake nodeHandshake => nodeHandshake,
        TaskHostHandshake taskHostHandshake => taskHostHandshake.ToHandshake(),
        WorkerNodeHandshake workerHandshake => workerHandshake,
        ServerNodeHandshake serverHandshake => serverHandshake,
        NetTaskHostHandshake netTaskHostHandshake => netTaskHostHandshake,
        FrameworkTaskHostHandshake frameworkTaskHostHandshake => frameworkTaskHostHandshake,
        Clr2TaskHostHandshake clr2TaskHostHandshake => clr2TaskHostHandshake,
        _ => Assumed.Unreachable<Handshake>("The handshake must be a known node-handshake variant."),
    };

    internal static bool IsHandshakeOptionEnabled(HandshakeOptions hostContext, HandshakeOptions option)
        => (hostContext & option) == option;

#if NET
    /// <summary>
    /// The .NET task host tolerates a worker that does not emit an architecture bit when the
    /// host is x64 or arm64. All other flags, including node reuse, must match to preserve
    /// task-host ownership. The upper byte is the handshake version and is ignored.
    /// </summary>
    internal static bool IsAllowedBitnessMismatch(int expectedOptions, int receivedOptions)
    {
        var expectedNodeType = (HandshakeOptions)(expectedOptions & 0x00FFFFFF);
        var receivedNodeType = (HandshakeOptions)(receivedOptions & 0x00FFFFFF);
        const HandshakeOptions ArchitectureFlags = HandshakeOptions.X64 | HandshakeOptions.Arm64;

        if ((expectedNodeType & ~ArchitectureFlags) != (receivedNodeType & ~ArchitectureFlags))
        {
            return false;
        }

        // An absent architecture bit is also the wire encoding for x86.
        bool receivedIsX86 = !IsHandshakeOptionEnabled(receivedNodeType, HandshakeOptions.X64) &&
                             !IsHandshakeOptionEnabled(receivedNodeType, HandshakeOptions.Arm64);

        bool expectedIsX64 = IsHandshakeOptionEnabled(expectedNodeType, HandshakeOptions.X64);
        bool expectedIsArm64 = IsHandshakeOptionEnabled(expectedNodeType, HandshakeOptions.Arm64);

        return receivedIsX86 && (expectedIsX64 || expectedIsArm64);
    }
#endif
}

/// <summary>
/// A task-host node can use only one of the three task-host protocols.
/// </summary>
internal union TaskHostHandshake(
    NetTaskHostHandshake,
    FrameworkTaskHostHandshake,
    Clr2TaskHostHandshake) : ILaunchableNodeHandshake
{
    internal TaskHostHandshakeOptions Options => this switch
    {
        NetTaskHostHandshake handshake => handshake.Options,
        FrameworkTaskHostHandshake handshake => handshake.Options,
        Clr2TaskHostHandshake handshake => handshake.Options,
    };

    internal TaskHostArchitecture Architecture => Options.Architecture;

    private INodeHandshake Protocol => this switch
    {
        NetTaskHostHandshake handshake => handshake,
        FrameworkTaskHostHandshake handshake => handshake,
        Clr2TaskHostHandshake handshake => handshake,
    };

    public HandshakeOptions HandshakeOptions => Protocol.HandshakeOptions;
    public bool NodeReuse => Options.NodeReuse;
    public HandshakeComponents RetrieveHandshakeComponents() => Protocol.RetrieveHandshakeComponents();
    public string GetKey() => Protocol.GetKey();
    public byte? ExpectedVersionInFirstByte => Protocol.ExpectedVersionInFirstByte;
    public bool IsNetTaskHostSaltMatch(int receivedSalt) => Protocol.IsNetTaskHostSaltMatch(receivedSalt);

    /// <summary>
    /// .NET and .NET Framework task hosts run as <see cref="NodeMode.OutOfProcTaskHostNode"/>.
    /// The CLR2 task host is a separate executable that has no node modes.
    /// </summary>
    public NodeMode? ExpectedNodeMode => this switch
    {
        NetTaskHostHandshake or FrameworkTaskHostHandshake => NodeMode.OutOfProcTaskHostNode,
        Clr2TaskHostHandshake => null,
    };

    internal Handshake ToHandshake() => this switch
    {
        NetTaskHostHandshake handshake => handshake,
        FrameworkTaskHostHandshake handshake => handshake,
        Clr2TaskHostHandshake handshake => handshake,
    };
}

internal enum TaskHostArchitecture
{
    Unspecified,
    X86,
    X64,
    Arm64,
}

/// <summary>
/// Task-host options before they are encoded as handshake flags.
/// </summary>
internal readonly record struct TaskHostHandshakeOptions(
    TaskHostArchitecture Architecture = TaskHostArchitecture.Unspecified,
    bool NodeReuse = false,
    bool LowPriority = false,
    bool Administrator = false,
    bool SidecarTaskHost = false)
{
    internal static TaskHostHandshakeOptions None => default;

    internal HandshakeOptions ToWireOptions()
    {
        HandshakeOptions options = Architecture switch
        {
            TaskHostArchitecture.Unspecified or TaskHostArchitecture.X86 => HandshakeOptions.None,
            TaskHostArchitecture.X64 => HandshakeOptions.X64,
            TaskHostArchitecture.Arm64 => HandshakeOptions.Arm64,
            _ => Assumed.Unreachable<HandshakeOptions>("Unsupported task-host architecture."),
        };

        if (NodeReuse)
        {
            options |= HandshakeOptions.NodeReuse;
        }

        if (LowPriority)
        {
            options |= HandshakeOptions.LowPriority;
        }

        if (Administrator)
        {
            options |= HandshakeOptions.Administrator;
        }

        if (SidecarTaskHost)
        {
            options |= HandshakeOptions.SidecarTaskHost;
        }

        return options;
    }

    // Unspecified and x86 have the same wire identity and must share reusable hosts.
    public bool Equals(TaskHostHandshakeOptions other) => ToWireOptions() == other.ToWireOptions();

    public override int GetHashCode() => (int)ToWireOptions();
}

internal sealed class WorkerNodeHandshake : NodeHandshake, ILaunchableNodeHandshake
{
    internal WorkerNodeHandshake(bool nodeReuse = false, bool lowPriority = false)
        : this(new CommunicationsUtilities.WorkerNodeHandshakeConfiguration(nodeReuse, lowPriority))
    {
    }

    internal WorkerNodeHandshake(CommunicationsUtilities.WorkerNodeHandshakeConfiguration configuration)
        : base(configuration.HandshakeOptions, includeSessionId: true, toolsDirectory: null)
    {
    }

    public NodeMode? ExpectedNodeMode => NodeMode.OutOfProcNode;
}

internal sealed class NetTaskHostHandshake : NodeHandshake
{
    private protected override bool IncludesChangeWave => false;
    private protected override bool AcceptsDriveLetterCaseVariant => true;

    internal TaskHostHandshakeOptions Options { get; }

    internal NetTaskHostHandshake(TaskHostHandshakeOptions options = default, string? toolsDirectory = null)
        : base(options.ToWireOptions() | HandshakeOptions.TaskHost | HandshakeOptions.NET, includeSessionId: true, toolsDirectory)
    {
        Options = options;
    }

    private protected override HandshakeComponents CreateComponents(int options, int salt, int sessionId, Version? fileVersion) => new(
        options,
        salt,
        Handshake.NetTaskHostHandshakeVersion,
        Handshake.NetTaskHostHandshakeVersion,
        Handshake.NetTaskHostHandshakeVersion,
        Handshake.NetTaskHostHandshakeVersion,
        sessionId);
}

internal sealed class FrameworkTaskHostHandshake : NodeHandshake
{
    private protected override bool IncludesChangeWave => false;

    internal TaskHostHandshakeOptions Options { get; }

    internal FrameworkTaskHostHandshake(TaskHostHandshakeOptions options = default)
        : base(options.ToWireOptions() | HandshakeOptions.TaskHost, includeSessionId: true, toolsDirectory: null)
    {
        Options = options;
    }

    internal FrameworkTaskHostHandshake(TaskHostHandshakeOptions options, string toolsDirectory, Version fileVersion)
        : base(options.ToWireOptions() | HandshakeOptions.TaskHost, includeSessionId: true, toolsDirectory, fileVersion)
    {
        Options = options;
    }
}

internal sealed class Clr2TaskHostHandshake : NodeHandshake
{
    private protected override bool IncludesChangeWave => false;

    internal TaskHostHandshakeOptions Options { get; }

    internal Clr2TaskHostHandshake(TaskHostHandshakeOptions options, string toolsDirectory, Version? fileVersion = null)
        : base(options.ToWireOptions() | HandshakeOptions.TaskHost | HandshakeOptions.CLR2, includeSessionId: true, toolsDirectory, fileVersion)
    {
        Options = options;
    }
}

/// <summary>
/// Shared wire encoding, deliberately unavailable as a node API parameter.
/// </summary>
internal abstract class NodeHandshake : INodeHandshake
{
    protected readonly HandshakeComponents _handshakeComponents;

    private protected virtual bool IncludesChangeWave => true;
    private protected virtual bool AcceptsDriveLetterCaseVariant => false;

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

    // Source options of the handshake.
    public HandshakeOptions HandshakeOptions { get; }
    public bool NodeReuse => (HandshakeOptions & global::Microsoft.Build.Internal.HandshakeOptions.NodeReuse) != 0;

    private protected NodeHandshake(HandshakeOptions nodeType, bool includeSessionId, string? toolsDirectory, Version? fileVersion = null)
    {
        HandshakeOptions = nodeType;

        toolsDirectory ??= BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryRoot;

        // Build handshake options with version in upper bits
        const int handshakeVersion = (int)CommunicationsUtilities.handshakeVersion;
        var options = (int)nodeType | (handshakeVersion << 24);
        CommunicationsUtilities.Trace($"Building handshake for node type {nodeType}, (version {handshakeVersion}): options {options}.");

        // Calculate salt from environment and tools directory
        string handshakeSalt = Environment.GetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT") ?? "";

        // A node resolves its change wave once and keeps it for the rest of its lifetime, so a node reused
        // by a later build would silently keep applying the change wave of the build that started it. Fold
        // the change wave into the salt so that a node which resolved a different change wave rejects the
        // connection and the parent starts a fresh node instead. Task hosts are excluded, see GetChangeWaveSalt.
        string changeWaveSalt = GetChangeWaveSalt();

        int salt = CommunicationsUtilities.GetHashCode($"{handshakeSalt}{toolsDirectory}{changeWaveSalt}");

        // The .NET task host parent (.NET Framework MSBuild, e.g. Visual Studio) and child
        // (.NET SDK MSBuild) compute this salt independently from different sources, and on Windows
        // those can differ only by drive-letter casing ("D:\..." vs "d:\..."). Because the salt is a
        // case-sensitive hash, that produces a mismatch and the otherwise-valid handshake fails with
        // MSB4216. Precompute the salt for the alternate drive-letter casing so the child also accepts
        // the parent's spelling (see IsNetTaskHostSaltMatch). Only the child consults this value.
        if (AcceptsDriveLetterCaseVariant && TryGetDriveLetterCaseVariant(toolsDirectory, out string? alternateToolsDirectory))
        {
            _netTaskHostSaltDriveCaseVariant = CommunicationsUtilities.GetHashCode($"{handshakeSalt}{alternateToolsDirectory}{changeWaveSalt}");
        }

        CommunicationsUtilities.Trace($"Handshake salt is {handshakeSalt}");
        CommunicationsUtilities.Trace($"Change wave salt is {changeWaveSalt}");
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

        _handshakeComponents = CreateComponents(options, salt, sessionId, fileVersion);
    }

    /// <summary>
    /// The part of the handshake salt that represents the change wave this process resolved, so that nodes
    /// which disagree about <c>MSBUILDDISABLEFEATURESFROMVERSION</c> never talk to each other. The resolved
    /// wave is used rather than the raw environment variable so that values which resolve to the same wave
    /// (for example an unset variable and one in an invalid format) still allow node reuse.
    /// <para>
    /// Returns an empty string for every task host, because a task host connection is the one place where the
    /// two sides can be different MSBuild versions and the resolved wave is a version-relative value:
    /// <see cref="ChangeWaves.DisabledWave"/> clamps and rounds the environment variable into the binary's own
    /// <c>AllWaves</c> list, which differs between versions. The CLR2 task host computes its handshake from a
    /// separate legacy copy of this code in MSBuildTaskHost.exe that knows nothing about change waves, and the
    /// .NET task host pairs a .NET Framework parent (e.g. Visual Studio) with a child from the installed .NET
    /// SDK, so the same variable can legitimately resolve to different waves on either side. Unlike a worker
    /// node or an MSBuild Server node, a task host mismatch is not self-healing: the parent can only relaunch
    /// the very same SDK binary, so the build fails with MSB4216 instead of starting a compatible host.
    /// </para>
    /// </summary>
    private string GetChangeWaveSalt()
    {
        if (!IncludesChangeWave)
        {
            return string.Empty;
        }

        Version disabledWave = ChangeWaves.DisabledWave;

        return disabledWave == ChangeWaves.EnableAllFeatures ? string.Empty : disabledWave.ToString();
    }

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

    private protected virtual HandshakeComponents CreateComponents(int options, int salt, int sessionId, Version? fileVersion)
    {
        // Read the file version from the assembly's AssemblyFileVersionAttribute rather than from the file on
        // disk: Assembly.Location is empty in a single-file/Native AOT host (and the on-disk read carries an
        // IL3000), while the attribute carries the same value and is preserved under trimming.
        fileVersion ??= new Version(
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
