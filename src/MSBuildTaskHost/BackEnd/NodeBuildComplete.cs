// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Build.TaskHost.BackEnd;

/// <summary>
/// The NodeBuildComplete packet is used to indicate to a node that it should clean up its current build and
/// possibly prepare for node reuse.
/// </summary>
internal sealed class NodeBuildComplete : INodePacket
{
    /// <summary>
    /// Flag indicating if the node should prepare for reuse after cleanup.
    /// </summary>
    private bool _prepareForReuse;

    public NodeBuildComplete(bool prepareForReuse)
    {
        _prepareForReuse = prepareForReuse;
    }

    private NodeBuildComplete()
    {
    }

    /// <summary>
    /// Gets a value indicating whether the node should prepare for reuse.
    /// </summary>
    public bool PrepareForReuse => _prepareForReuse;

    /// <summary>
    /// Gets the packet type.
    /// </summary>
    public NodePacketType Type => NodePacketType.NodeBuildComplete;

    /// <summary>
    /// Translates the packet to/from binary form.
    /// </summary>
    /// <param name="translator">The translator to use.</param>
    public void Translate(ITranslator translator)
        => translator.Translate(ref _prepareForReuse);

    internal static NodeBuildComplete FactoryForDeserialization(ITranslator translator)
    {
        var packet = new NodeBuildComplete();
        packet.Translate(translator);
        return packet;
    }
}
