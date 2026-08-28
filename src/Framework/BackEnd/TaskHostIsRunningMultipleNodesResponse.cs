// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Response packet from owning worker node to TaskHost with the IsRunningMultipleNodes value.
    /// </summary>
    internal class TaskHostIsRunningMultipleNodesResponse : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private bool _isRunningMultipleNodes;

        public TaskHostIsRunningMultipleNodesResponse()
        {
        }

        public TaskHostIsRunningMultipleNodesResponse(int requestId, bool isRunningMultipleNodes)
        {
            _requestId = requestId;
            _isRunningMultipleNodes = isRunningMultipleNodes;
        }

        public NodePacketType Type => NodePacketType.TaskHostIsRunningMultipleNodesResponse;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        public bool IsRunningMultipleNodes => _isRunningMultipleNodes;

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
            translator.Translate(ref _isRunningMultipleNodes);
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostIsRunningMultipleNodesResponse();
            packet.Translate(translator);
            return packet;
        }
    }
}
