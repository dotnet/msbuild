﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Reasons why the node shut down.
    /// </summary>
    internal enum NodeShutdownReason
    {
        /// <summary>
        /// The node shut down because it was requested to shut down.
        /// </summary>
        Requested,

        /// <summary>
        /// The node shut down because of an error.
        /// </summary>
        Error,

        /// <summary>
        /// The node shut down because the connection failed.
        /// </summary>
        ConnectionFailed,
    }

    /// <summary>
    /// Implementation of INodePacket for the packet informing the build manager than a node has shut down.
    /// This is the last packet the BuildManager will receive from a Node, and as such can be used to trigger
    /// any appropriate cleanup behavior.
    /// </summary>
    internal class NodeShutdown : INodePacket
    {
        /// <summary>
        /// The reason the node shut down.
        /// </summary>
        private NodeShutdownReason _reason;

        /// <summary>
        /// The exception - if any.
        /// </summary>
        private Exception _exception;

        /// <summary>
        /// Constructor
        /// </summary>
        public NodeShutdown(NodeShutdownReason reason)
            : this(reason, null)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public NodeShutdown(NodeShutdownReason reason, Exception e)
        {
            _reason = reason;
            _exception = e;
        }

        /// <summary>
        /// Constructor for deserialization
        /// </summary>
        private NodeShutdown()
        {
        }

        #region INodePacket Members

        /// <summary>
        /// Returns the packet type.
        /// </summary>
        public NodePacketType Type
        {
            get { return NodePacketType.NodeShutdown; }
        }

        #endregion

        /// <summary>
        /// The reason for shutting down.
        /// </summary>
        public NodeShutdownReason Reason
        {
            get { return _reason; }
        }

        /// <summary>
        /// The exception, if any.
        /// </summary>
        public Exception Exception
        {
            get { return _exception; }
        }

        #region INodePacketTranslatable Members

        /// <summary>
        /// Serializes or deserializes a packet.
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.TranslateEnum(ref _reason, (int)_reason);
            translator.TranslateException(ref _exception);
        }

        /// <summary>
        /// Factory method for deserialization
        /// </summary>
        internal static NodeShutdown FactoryForDeserialization(ITranslator translator)
        {
            NodeShutdown shutdown = new NodeShutdown();
            shutdown.Translate(translator);
            return shutdown;
        }

        #endregion
    }
}
