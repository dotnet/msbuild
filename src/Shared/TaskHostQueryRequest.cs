// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !CLR2COMPATIBILITY

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Packet sent from TaskHost to parent to query simple build engine state.
    /// </summary>
    internal class TaskHostQueryRequest : INodePacket, ITaskHostCallbackPacket
    {
        private QueryType _queryType;
        private int _requestId;

        public TaskHostQueryRequest()
        {
        }

        public TaskHostQueryRequest(QueryType queryType)
        {
            _queryType = queryType;
        }

        public NodePacketType Type => NodePacketType.TaskHostQueryRequest;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        public QueryType Query => _queryType;

        public void Translate(ITranslator translator)
        {
            translator.TranslateEnum(ref _queryType, (int)_queryType);
            translator.Translate(ref _requestId);
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostQueryRequest();
            packet.Translate(translator);
            return packet;
        }

        internal enum QueryType
        {
            IsRunningMultipleNodes = 0,
        }
    }
}

#endif
