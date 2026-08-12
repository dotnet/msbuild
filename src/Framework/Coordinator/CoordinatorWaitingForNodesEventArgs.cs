// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

internal sealed class CoordinatorWaitingForNodesEventArgs : BuildMessageEventArgs, IExtendedBuildEventArgs
{
    /// <inheritdoc />
    string IExtendedBuildEventArgs.ExtendedType
    {
        get => Constants.WaitingForNodesEventType;
        set { /* Type is fixed for this event. */ }
    }

    public Dictionary<string, string?>? ExtendedMetadata { get; set; }

    public string? ExtendedData { get; set; }

    internal CoordinatorWaitingForNodesEventArgs(string? message, string? senderName, MessageImportance importance)
        : base(message, helpKeyword: null, senderName, importance)
    {
    }

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);
        writer.WriteExtendedBuildEventData(this);
    }
}
