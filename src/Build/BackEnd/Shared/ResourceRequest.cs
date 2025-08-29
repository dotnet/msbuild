﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This packet is sent by a node to request or release resources from/to the scheduler.
    /// </summary>
    internal sealed class ResourceRequest : INodePacket
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
        /// Private constructor, use CreateAcquireRequest or CreateReleaseRequest to make instances.
        /// </summary>
        private ResourceRequest(bool isResourceAcquire, int globalRequestId, int numCores, bool isBlocking)
        {
            _isResourceAcquire = isResourceAcquire;
            _isBlocking = isBlocking;
            _globalRequestId = globalRequestId;
            _numCores = numCores;
        }

        /// <summary>
        /// Factory method for acquiring.
        /// </summary>
        public static ResourceRequest CreateAcquireRequest(int globalRequestId, int numCores, bool isBlocking)
            => new ResourceRequest(isResourceAcquire: true, globalRequestId, numCores, isBlocking);

        /// <summary>
        /// Factory method for releasing.
        /// </summary>
        public static ResourceRequest CreateReleaseRequest(int globalRequestId, int numCores)
            => new ResourceRequest(isResourceAcquire: false, globalRequestId, numCores, isBlocking: false);

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
