// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal sealed class RarNodeExecuteResponse : INodePacket
    {
        public NodePacketType Type => NodePacketType.RarNodeExecuteResponse;

        public void Translate(ITranslator translator)
        {
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            RarNodeExecuteResponse response = new();
            response.Translate(translator);
            return response;
        }
    }
}
