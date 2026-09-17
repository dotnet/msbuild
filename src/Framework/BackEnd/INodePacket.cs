// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

/// <summary>
/// This interface represents a packet which may be transmitted using an INodeEndpoint.
/// Implementations define the serialized form of the data.
/// </summary>
internal interface INodePacket : ITranslatable
{
    /// <summary>
    /// The type of the packet.  Used to reconstitute the packet using the correct factory.
    /// </summary>
    NodePacketType Type { get; }
}
