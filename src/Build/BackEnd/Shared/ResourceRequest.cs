// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// </summary>
    internal class ResourceRequest : INodePacket
    {
        /// <summary>
        /// The global request id of the request which is asking for resources.
        /// </summary>
        private int _blockedGlobalRequestId;

        private bool _isAcquire;

        private int _numCores;

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        internal ResourceRequest(ITranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Acquire
        /// </summary>
        internal ResourceRequest(int blockedGlobalRequestId, int numCores)
        {
            _blockedGlobalRequestId = blockedGlobalRequestId;
            _isAcquire = true;
            _numCores = numCores;
        }

        /// <summary>
        /// Release
        /// </summary>
        internal ResourceRequest(int numCores)
        {
            _isAcquire = false;
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
        public bool IsAcquire
        {
            [DebuggerStepThrough]
            get
            {
                return _isAcquire;
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
            translator.Translate(ref _isAcquire);
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
