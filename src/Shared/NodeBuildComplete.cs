// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The NodeBuildComplete packet is used to indicate to a node that it should clean up its current build and
    /// possibly prepare for node reuse.
    /// </summary>
    internal class NodeBuildComplete : INodePacket2
    {
        /// <summary>
        /// Flag indicating if the node should prepare for reuse after cleanup.
        /// </summary>
        private bool _prepareForReuse;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NodeBuildComplete(bool prepareForReuse)
        {
            _prepareForReuse = prepareForReuse;
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
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static NodeBuildComplete FactoryForDeserialization(ITranslatorBase translator)
        {
            NodeBuildComplete packet = new NodeBuildComplete();

            if (translator.Protocol == ProtocolType.Binary)
            {
                packet.Translate((ITranslator)translator);
            }
#if !TASKHOST
            else
            {
                packet.Translate((IJsonTranslator)translator);
            }
#endif
       
            return packet;
        }

#if !TASKHOST
        public void Translate(IJsonTranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var model = new NodeBuildCompleteModel(_prepareForReuse);
                translator.TranslateToJson(model);
            }
            else // ReadFromStream
            {
                var model = translator.TranslateFromJson<NodeBuildCompleteModel>();
                _prepareForReuse = model.prepareForReuse;
            }
        }

        internal record NodeBuildCompleteModel(bool prepareForReuse);

#endif

        #endregion
    }
}
