// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !CLR2COMPATIBILITY

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Packet sent from TaskHost to parent for RequestCores/ReleaseCores operations.
    /// </summary>
    internal sealed class TaskHostResourceRequest : INodePacket, ITaskHostCallbackPacket
    {
        private ResourceOperation _operation;
        private int _coreCount;
        private int _requestId;

        public TaskHostResourceRequest()
        {
        }

        public TaskHostResourceRequest(ResourceOperation operation, int coreCount)
        {
            _operation = operation;
            _coreCount = coreCount;
        }

        public NodePacketType Type => NodePacketType.TaskHostResourceRequest;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        public ResourceOperation Operation => _operation;

        public int CoreCount => _coreCount;

        public void Translate(ITranslator translator)
        {
            translator.TranslateEnum(ref _operation, (int)_operation);
            translator.Translate(ref _coreCount);
            translator.Translate(ref _requestId);
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostResourceRequest();
            packet.Translate(translator);
            return packet;
        }

        internal enum ResourceOperation
        {
            RequestCores = 0,
            ReleaseCores = 1,
        }
    }
}

#endif
