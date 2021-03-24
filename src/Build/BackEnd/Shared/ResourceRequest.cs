// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

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
        private int _blockedGlobalRequestId;

        private bool _isResourceAcquire;

        private bool _isBlocking;

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
        internal ResourceRequest(int blockedGlobalRequestId, int numCores, bool isBlocking)
        {
            _isResourceAcquire = true;
            _isBlocking = isBlocking;
            _blockedGlobalRequestId = blockedGlobalRequestId;
            _numCores = numCores;
        }

        /// <summary>
        /// Constructor for releasing.
        /// </summary>
        internal ResourceRequest(int blockedGlobalRequestId, int numCores)
        {
            _isResourceAcquire = false;
            _blockedGlobalRequestId = blockedGlobalRequestId;
            _numCores = numCores;
        }

        /// <summary>
        /// Returns the type of packet.
        /// </summary>
        public NodePacketType Type
        {
            [DebuggerStepThrough]
            get
            { return NodePacketType.ResourceRequest; }
        }

        /// <summary>
        /// Accessor for the blocked request id.
        /// </summary>
        public int BlockedRequestId
        {
            [DebuggerStepThrough]
            get
            {
                return _blockedGlobalRequestId;
            }
        }

        /// <summary>
        /// </summary>
        public bool IsResourceAcquire
        {
            [DebuggerStepThrough]
            get
            {
                return _isResourceAcquire;
            }
        }

        /// <summary>
        /// </summary>
        public bool IsBlocking
        {
            [DebuggerStepThrough]
            get
            {
                return _isBlocking;
            }
        }

        /// <summary>
        /// </summary>
        public int NumCores
        {
            [DebuggerStepThrough]
            get
            {
                return _numCores;
            }
        }

        #region INodePacketTranslatable Members

        /// <summary>
        /// Serialization method.
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _blockedGlobalRequestId);
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
