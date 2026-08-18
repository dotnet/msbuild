// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Packet sent from TaskHost to owning worker node to query IsRunningMultipleNodes.
    /// </summary>
    internal class TaskHostIsRunningMultipleNodesRequest : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;

        public TaskHostIsRunningMultipleNodesRequest()
        {
        }

        public NodePacketType Type => NodePacketType.TaskHostIsRunningMultipleNodesRequest;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostIsRunningMultipleNodesRequest();
            packet.Translate(translator);
            return packet;
        }
    }
}
