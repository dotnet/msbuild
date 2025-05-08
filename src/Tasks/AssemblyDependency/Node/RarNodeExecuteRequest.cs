// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal sealed class RarNodeExecuteRequest : INodePacket
    {
        public NodePacketType Type => NodePacketType.RarNodeExecuteRequest;

        public void Translate(ITranslator translator)
        {
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            RarNodeExecuteRequest request = new();
            request.Translate(translator);
            return request;
        }
    }
}
