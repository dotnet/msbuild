// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
/// Raised by the build coordinator while it is waiting for the coordination server to grant it worker nodes.
/// </summary>
/// <remarks>
/// Consumers (e.g. <c>TerminalLogger</c>) should recognize this diagnostic by its concrete type
/// (<c>is CoordinatorWaitingForNodesEventArgs</c>) rather than by comparing its rendered, localizable message
/// text. The concrete type is preserved for in-process consumers, i.e. loggers registered directly with the
/// <c>LoggingService</c> in the process that raised the event -- which is the only place this event is
/// currently raised or consumed. If the event ever crossed a node boundary (out-of-proc IPC) or were
/// round-tripped through a binary log, it would be reconstructed generically as
/// <see cref="ExtendedBuildMessageEventArgs"/> (still carrying the same <see cref="IExtendedBuildEventArgs.ExtendedType"/>), since
/// neither the node-IPC packet format nor the binary log format preserve concrete
/// <see cref="BuildMessageEventArgs"/> subtypes that aren't explicitly registered with them.
/// </remarks>
public sealed class CoordinatorWaitingForNodesEventArgs : BuildMessageEventArgs, IExtendedBuildEventArgs
{
    /// <inheritdoc />
    string IExtendedBuildEventArgs.ExtendedType
    {
        get => Constants.WaitingForNodesEventType;
        set { /* Type is fixed for this event; ignore deserialized value which is always expected to match. */ }
    }

    /// <inheritdoc />
    public Dictionary<string, string?>? ExtendedMetadata { get; set; }

    /// <inheritdoc />
    public string? ExtendedData { get; set; }

    /// <summary>
    /// Default constructor. Used for deserialization.
    /// </summary>
    internal CoordinatorWaitingForNodesEventArgs()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CoordinatorWaitingForNodesEventArgs"/> class.
    /// </summary>
    /// <param name="message">Text message.</param>
    /// <param name="senderName">Name of event sender.</param>
    /// <param name="importance">Importance of the message.</param>
    public CoordinatorWaitingForNodesEventArgs(string? message, string? senderName, MessageImportance importance)
        : base(message, helpKeyword: null, senderName, importance)
    {
    }

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);
        writer.WriteExtendedBuildEventData(this);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        reader.ReadExtendedBuildEventData(this);
    }
}
