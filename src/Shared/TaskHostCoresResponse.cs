// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Response packet from owning worker node to TaskHost with the core allocation result.
    /// For RequestCores: <see cref="GrantedCores"/> is the number of cores granted.
    /// For ReleaseCores: <see cref="GrantedCores"/> is 0 (acknowledgment only).
    /// </summary>
    internal class TaskHostCoresResponse : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private int _grantedCores;

        public TaskHostCoresResponse()
        {
        }

        public TaskHostCoresResponse(int requestId, int grantedCores)
        {
            _requestId = requestId;
            _grantedCores = grantedCores;
        }

        public NodePacketType Type => NodePacketType.TaskHostCoresResponse;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        /// <summary>
        /// The number of cores granted by the scheduler.
        /// For ReleaseCores responses, this is 0.
        /// </summary>
        public int GrantedCores => _grantedCores;

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
            translator.Translate(ref _grantedCores);
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostCoresResponse();
            packet.Translate(translator);
            return packet;
        }
    }
}
