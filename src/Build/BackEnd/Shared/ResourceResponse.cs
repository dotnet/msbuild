// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// </summary>
    internal class ResourceResponse : INodePacket
    {
        /// <summary>
        /// The global request id of the request which is being responded to.
        /// </summary>
        private int _globalRequestId;

        /// <summary>
        /// Number of CPU cores being granted.
        /// </summary>
        private int _numCores;

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        internal ResourceResponse(ITranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Constructor for granting cores.
        /// </summary>
        internal ResourceResponse(int globalRequestId, int numCores)
        {
            _globalRequestId = globalRequestId;
            _numCores = numCores;
        }

        /// <summary>
        /// Returns the type of packet.
        /// </summary>
        public NodePacketType Type => NodePacketType.ResourceResponse;

        /// <summary>
        /// Accessor for the global request id.
        /// </summary>
        public int GlobalRequestId => _globalRequestId;

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
