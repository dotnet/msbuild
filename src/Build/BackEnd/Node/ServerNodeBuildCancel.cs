// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
