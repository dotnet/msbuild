// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// </summary>
    internal class ResourceResponse : INodePacket
    {
        /// <summary>
        /// The global request id of the request which is being responded to.
        /// </summary>
        private int _blockedGlobalRequestId;

        private int _numCores;

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        internal ResourceResponse(ITranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// </summary>
        internal ResourceResponse(int blockedGlobalRequestId, int numCores)
        {
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
            { return NodePacketType.ResourceResponse; }
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
            translator.Translate(ref _numCores);
        }

        #endregion

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new ResourceResponse(translator);
        }
    }
}
