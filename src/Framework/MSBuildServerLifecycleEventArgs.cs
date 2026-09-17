// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;

namespace Microsoft.Build.Framework;

/// <summary>
/// Identifies how a build related to the MSBuild Server node.
/// </summary>
public enum MSBuildServerLifecycleKind
{
    /// <summary>
    /// A fresh MSBuild Server node was spawned to serve this build.
    /// </summary>
    Spawned,

    /// <summary>
    /// An already-running MSBuild Server node was reused for this build.
    /// </summary>
    Reused,

    /// <summary>
    /// MSBuild Server was requested but not used; the build ran in-process instead.
    /// </summary>
    NotUsed,
}

/// <summary>
/// Records how a build related to the MSBuild Server node (spawned, reused, or requested but not used).
/// This is a dedicated, versionable event type rather than an ad-hoc message so that tooling can recognize
/// and render it structurally. It is written to the binary log under its own record kind; forward-compatible
/// binary-log readers that predate the record kind skip it via the length-prefixed record framing.
/// </summary>
public sealed class MSBuildServerLifecycleEventArgs : BuildMessageEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MSBuildServerLifecycleEventArgs"/> class. Used for
    /// deserialization.
    /// </summary>
    internal MSBuildServerLifecycleEventArgs()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MSBuildServerLifecycleEventArgs"/> class.
    /// </summary>
    /// <param name="kind">How the build related to the MSBuild Server node.</param>
    /// <param name="processId">The MSBuild Server node's process id (0 when not applicable, e.g. for <see cref="MSBuildServerLifecycleKind.NotUsed"/>).</param>
    /// <param name="reason">A localized, human-readable reason the server was not used (for <see cref="MSBuildServerLifecycleKind.NotUsed"/>); otherwise <see langword="null"/>.</param>
    /// <param name="reasonCode">A stable, non-localized code for the fall-back cause (for <see cref="MSBuildServerLifecycleKind.NotUsed"/>); otherwise <see langword="null"/>.</param>
    /// <param name="message">The localized, human-readable message describing the event.</param>
    /// <param name="importance">The importance of the message.</param>
    /// <param name="shortLived"><see langword="true"/> when a spawned server will shut down after this build
    /// instead of staying resident for reuse (a "short-lived" server).</param>
    public MSBuildServerLifecycleEventArgs(
        MSBuildServerLifecycleKind kind,
        int processId,
        string? reason,
        string? reasonCode,
        string? message,
        MessageImportance importance = MessageImportance.Low,
        bool shortLived = false)
        : base(message, helpKeyword: null, senderName: "MSBuild", importance, DateTime.UtcNow)
    {
        Kind = kind;
        ProcessId = processId;
        Reason = reason;
        ReasonCode = reasonCode;
        ShortLived = shortLived;
    }

    /// <summary>
    /// How the build related to the MSBuild Server node.
    /// </summary>
    public MSBuildServerLifecycleKind Kind { get; private set; }

    /// <summary>
    /// <see langword="true"/> when a spawned server node will tear itself down after this build instead of
    /// staying resident for reuse (a "short-lived" server — a <c>/mt</c> build with node reuse off).
    /// </summary>
    public bool ShortLived { get; private set; }

    /// <summary>
    /// The MSBuild Server node's process id, or 0 when not applicable.
    /// </summary>
    public int ProcessId { get; private set; }

    /// <summary>
    /// A localized, human-readable reason the server was not used (for
    /// <see cref="MSBuildServerLifecycleKind.NotUsed"/>); otherwise <see langword="null"/>.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// A stable, non-localized code identifying the fall-back cause (for
    /// <see cref="MSBuildServerLifecycleKind.NotUsed"/>); otherwise <see langword="null"/>.
    /// </summary>
    public string? ReasonCode { get; private set; }

    // This node-packet serializer and CreateFromStream below MUST stay in field order/count sync with each
    // other AND with the binary-log serializer in Microsoft.Build.Logging.BuildEventArgsWriter.Write(
    // MSBuildServerLifecycleEventArgs) / BuildEventArgsReader.ReadMSBuildServerLifecycleEventArgs. The two
    // paths use different-but-equivalent primitives: here Write7BitEncodedInt + WriteOptionalString; the
    // binlog path uses BuildEventArgsWriter.Write(int) (also 7-bit) + WriteDeduplicatedString.
    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.Write7BitEncodedInt((int)Kind);
        writer.Write7BitEncodedInt(ProcessId);
        writer.WriteOptionalString(Reason);
        writer.WriteOptionalString(ReasonCode);
        writer.Write(ShortLived);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        Kind = (MSBuildServerLifecycleKind)reader.Read7BitEncodedInt();
        ProcessId = reader.Read7BitEncodedInt();
        Reason = reader.ReadOptionalString();
        ReasonCode = reader.ReadOptionalString();
        ShortLived = reader.ReadBoolean();
    }
}
