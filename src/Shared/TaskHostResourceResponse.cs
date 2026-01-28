// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !CLR2COMPATIBILITY

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Response packet from parent to TaskHost for resource requests.
    /// </summary>
    internal sealed class TaskHostResourceResponse : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private int _coresGranted;

        public TaskHostResourceResponse()
        {
        }

        public TaskHostResourceResponse(int requestId, int coresGranted)
        {
            _requestId = requestId;
            _coresGranted = coresGranted;
        }

        public NodePacketType Type => NodePacketType.TaskHostResourceResponse;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        /// <summary>
        /// Number of cores granted by the scheduler. For ReleaseCores operations, this is just an acknowledgment.
        /// </summary>
        public int CoresGranted => _coresGranted;

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
            translator.Translate(ref _coresGranted);
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostResourceResponse();
            packet.Translate(translator);
            return packet;
        }
    }
}

#endif
