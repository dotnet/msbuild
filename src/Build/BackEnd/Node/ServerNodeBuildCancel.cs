// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    internal sealed class ServerNodeBuildCancel : INodePacket
    {
        public NodePacketType Type => NodePacketType.ServerNodeBuildCancel;

        public void Translate(ITranslator translator)
        {
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new ServerNodeBuildCancel();
        }
    }
}
