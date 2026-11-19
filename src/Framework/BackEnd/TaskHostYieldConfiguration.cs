// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Sets the parent's yield policy before the next task configuration, including on reused hosts.
/// </summary>
internal sealed class TaskHostYieldConfiguration(bool enabled) : INodePacket
{
    private bool _enabled = enabled;

    public bool Enabled => _enabled;

    public NodePacketType Type => NodePacketType.TaskHostYieldConfiguration;

    public void Translate(ITranslator translator) => translator.Translate(ref _enabled);

    internal static INodePacket FactoryForDeserialization(ITranslator translator)
    {
        TaskHostYieldConfiguration packet = new(false);
        packet.Translate(translator);
        return packet;
    }
}
