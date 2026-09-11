// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;


namespace Microsoft.Build.BackEnd
{
    internal enum NodeBuildCompleteAction
    {
        Legacy,
        ReuseWithConnection,
        Shutdown
    }

    /// <summary>
    /// The NodeBuildComplete packet is used to indicate to a node that it should clean up its current build and
    /// possibly prepare for node reuse.
    /// </summary>
    internal class NodeBuildComplete : INodePacket
    {
        /// <summary>
        /// Flag indicating if the node should prepare for reuse after cleanup.
        /// </summary>
        private bool _prepareForReuse;
        private NodeBuildCompleteAction _action;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NodeBuildComplete(bool prepareForReuse, NodeBuildCompleteAction action = NodeBuildCompleteAction.Legacy)
        {
            _prepareForReuse = prepareForReuse;
            _action = action;
        }

        /// <summary>
        /// Private constructor for translation
        /// </summary>
        private NodeBuildComplete()
        {
        }

        /// <summary>
        /// Flag indicating if the node should prepare for reuse.
        /// </summary>
        public bool PrepareForReuse
        {
            [DebuggerStepThrough]
            get
            { return _prepareForReuse; }
        }

        internal NodeBuildCompleteAction Action => _action;

        #region INodePacket Members

        /// <summary>
        /// The packet type
        /// </summary>
        public NodePacketType Type
        {
            [DebuggerStepThrough]
            get
            { return NodePacketType.NodeBuildComplete; }
        }

        #endregion

        #region INodePacketTranslatable Members

        /// <summary>
        /// Translates the packet to/from binary form.
        /// </summary>
        /// <param name="translator">The translator to use.</param>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _prepareForReuse);
            if (translator.NegotiatedPacketVersion >= NodePacketTypeExtensions.TaskHostOwnershipMinVersion)
            {
                translator.TranslateEnum(ref _action, (int)_action);
            }
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static NodeBuildComplete FactoryForDeserialization(ITranslator translator)
        {
            NodeBuildComplete packet = new NodeBuildComplete();
            packet.Translate(translator);
            return packet;
        }

        #endregion
    }
}
