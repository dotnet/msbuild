// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
/// Raised by the build coordinator while it is waiting for the coordination server to grant it worker nodes.
/// </summary>
/// <remarks>
/// Recognize this diagnostic by its concrete type (<c>is CoordinatorWaitingForNodesEventArgs</c>), not by
/// comparing message text. Note: the concrete type is only preserved for in-process consumers; if this event
/// ever crossed a node boundary or was replayed from a binary log, it would come back as a generic
/// <see cref="ExtendedBuildMessageEventArgs"/> with the same <see cref="IExtendedBuildEventArgs.ExtendedType"/>.
/// </remarks>
public sealed class CoordinatorWaitingForNodesEventArgs : BuildMessageEventArgs, IExtendedBuildEventArgs
{
    /// <inheritdoc />
    string IExtendedBuildEventArgs.ExtendedType
    {
        get => Constants.WaitingForNodesEventType;
        set { /* Type is fixed for this event. */ }
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
