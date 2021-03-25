// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This packet is sent by a node to request or release resources from/to the scheduler.
    /// </summary>
    internal class ResourceRequest : INodePacket
    {
        /// <summary>
        /// The global request id of the request which is asking for resources.
        /// </summary>
        private int _globalRequestId;

        /// <summary>
        /// True if this is a request to acquire resources, false if this is a request to release resources.
        /// </summary>
        private bool _isResourceAcquire;

        /// <summary>
        /// True if the request should be blocking until the resources become available. False if the request should
        /// be responded to immediately even if the desired resources are not available.
        /// </summary>
        private bool _isBlocking;

        /// <summary>
        /// Number of CPU cores being requested or released.
        /// </summary>
        private int _numCores;

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        internal ResourceRequest(ITranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Constructor for acquiring.
        /// </summary>
        internal ResourceRequest(int globalRequestId, int numCores, bool isBlocking)
        {
            _isResourceAcquire = true;
            _isBlocking = isBlocking;
            _globalRequestId = globalRequestId;
            _numCores = numCores;
        }

        /// <summary>
        /// Constructor for releasing.
        /// </summary>
        internal ResourceRequest(int globalRequestId, int numCores)
        {
            _isResourceAcquire = false;
            _globalRequestId = globalRequestId;
            _numCores = numCores;
        }

        /// <summary>
        /// Returns the type of packet.
        /// </summary>
        public NodePacketType Type => NodePacketType.ResourceRequest;

        /// <summary>
        /// Accessor for the global request id.
        /// </summary>
        public int GlobalRequestId => _globalRequestId;

        /// <summary>
        /// Accessor for _isResourceAcquire.
        /// </summary>
        public bool IsResourceAcquire => _isResourceAcquire;

        /// <summary>
        /// Accessor fro _isBlocking.
        /// </summary>
        public bool IsBlocking => _isBlocking;

        /// <summary>
        /// Accessor for _numCores.
        /// </summary>
        public int NumCores => _numCores;

        #region INodePacketTranslatable Members

        /// <summary>
        /// Serialization method.
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _globalRequestId);
            translator.Translate(ref _isResourceAcquire);
            translator.Translate(ref _isBlocking);
            translator.Translate(ref _numCores);
        }

        #endregion

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new ResourceRequest(translator);
        }
    }
}
