// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Packet sent from TaskHost to owning worker node for RequestCores/ReleaseCores operations.
    /// When <see cref="IsRelease"/> is false, this is a RequestCores call with <see cref="RequestedCores"/> cores requested.
    /// When <see cref="IsRelease"/> is true, this is a ReleaseCores call with <see cref="RequestedCores"/> cores to release.
    /// </summary>
    internal class TaskHostCoresRequest : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private int _requestedCores;
        private bool _isRelease;

        public TaskHostCoresRequest()
        {
        }

        public TaskHostCoresRequest(int requestedCores, bool isRelease)
        {
            _requestedCores = requestedCores;
            _isRelease = isRelease;
        }

        public NodePacketType Type => NodePacketType.TaskHostCoresRequest;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        /// <summary>
        /// The number of cores requested (for RequestCores) or cores to release (for ReleaseCores).
        /// </summary>
        public int RequestedCores => _requestedCores;

        /// <summary>
        /// True if this is a ReleaseCores operation, false if RequestCores.
        /// </summary>
        public bool IsRelease => _isRelease;

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
            translator.Translate(ref _requestedCores);
            translator.Translate(ref _isRelease);
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostCoresRequest();
            packet.Translate(translator);
            return packet;
        }
    }
}
