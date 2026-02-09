// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Response packet from parent to TaskHost for query requests.
    /// </summary>
    internal class TaskHostQueryResponse : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private bool _boolResult;

        public TaskHostQueryResponse()
        {
        }

        public TaskHostQueryResponse(int requestId, bool boolResult)
        {
            _requestId = requestId;
            _boolResult = boolResult;
        }

        public NodePacketType Type => NodePacketType.TaskHostQueryResponse;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        public bool BoolResult => _boolResult;

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
            translator.Translate(ref _boolResult);
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostQueryResponse();
            packet.Translate(translator);
            return packet;
        }
    }
}
