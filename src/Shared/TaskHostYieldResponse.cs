// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Response from worker node to TaskHost acknowledging a Reacquire request.
    /// Sent only for <see cref="YieldOperation.Reacquire"/>; Yield is fire-and-forget.
    /// </summary>
    internal class TaskHostYieldResponse : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;

        public TaskHostYieldResponse()
        {
        }

        public TaskHostYieldResponse(int requestId)
        {
            _requestId = requestId;
        }

        public NodePacketType Type => NodePacketType.TaskHostYieldResponse;

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
            var packet = new TaskHostYieldResponse();
            packet.Translate(translator);
            return packet;
        }
    }
}
