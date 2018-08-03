// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This is the packet which is sent in response to a build configuration ID request.  When the node generates a new configuration which it has
    /// never seen before, it gives that configuration a temporary, "unresolved" configuration id.  The node then asks the Build Request Manager
    /// for the "resolved" configuration id, which is global to all nodes in the system.  This packet maps the unresolved to the resolved
    /// configuration id.  Once this packet is received, the node engine can then continue processing requests associated with the configuration.
    /// </summary>
    internal class BuildRequestConfigurationResponse : INodePacket
    {
        /// <summary>
        /// The configuration ID assigned by the node
        /// </summary>
        private int _nodeConfigId;

        /// <summary>
        /// The configuration ID assigned by the build manager.
        /// </summary>
        private int _globalConfigId;

        /// <summary>
        /// The results node assigned to this configuration
        /// </summary>
        private int _resultsNodeId;

        /// <summary>
        /// Constructor for non-deserialization initialization.
        /// </summary>
        /// <param name="nodeConfigId">The node-assigned configuration id</param>
        /// <param name="globalConfigId">The build manager-assigned configuration id</param>
        /// <param name="resultsNodeId">The result node identifier.</param>
        public BuildRequestConfigurationResponse(int nodeConfigId, int globalConfigId, int resultsNodeId)
        {
            _nodeConfigId = nodeConfigId;
            _globalConfigId = globalConfigId;
            _resultsNodeId = resultsNodeId;
        }

        /// <summary>
        /// Constructor for deserialization
        /// </summary>
        private BuildRequestConfigurationResponse(INodePacketTranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Returns the node-assigned configuration id
        /// </summary>
        public int NodeConfigurationId => _nodeConfigId;

        /// <summary>
        /// Returns the build manager assigned configuration id
        /// </summary>
        public int GlobalConfigurationId => _globalConfigId;

        /// <summary>
        /// Returns the results node for the global configuration.
        /// </summary>
        public int ResultsNodeId => _resultsNodeId;

        #region INodePacket Members

        /// <summary>
        /// INodePacket property.  Returns the packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.BuildRequestConfigurationResponse;

        /// <summary>
        /// Reads/writes this packet
        /// </summary>
        public void Translate(INodePacketTranslator translator)
        {
            translator.Translate(ref _nodeConfigId);
            translator.Translate(ref _globalConfigId);
            translator.Translate(ref _resultsNodeId);
        }

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(INodePacketTranslator translator)
        {
            return new BuildRequestConfigurationResponse(translator);
        }

        #endregion
    }
}
