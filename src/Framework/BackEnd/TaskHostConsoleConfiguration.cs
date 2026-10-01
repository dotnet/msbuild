// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Enables task-host console forwarding for the current build.
    /// </summary>
    internal sealed class TaskHostConsoleConfiguration : INodePacket
    {
        public NodePacketType Type => NodePacketType.TaskHostConsoleConfiguration;

        public void Translate(ITranslator translator)
        {
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new TaskHostConsoleConfiguration();
        }
    }
}
