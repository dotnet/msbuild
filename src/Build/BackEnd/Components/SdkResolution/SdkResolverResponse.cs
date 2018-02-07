// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// Represents an SDK resolver response sent between nodes.  This is mostly a wrapper around an <see cref="SdkResult"/>
    /// with an additional <see cref="INodePacket"/> implementation.
    /// </summary>
    internal sealed class SdkResolverResponse : INodePacket
    {
        private string _error;
        private string _fullPath;
        private string _version;

        public SdkResolverResponse(string fullPath, string version)
        {
            _fullPath = fullPath;
            _version = version;
        }

        public SdkResolverResponse(INodePacketTranslator translator)
        {
            Translate(translator);
        }

        public SdkResolverResponse(Exception exception)
        {
            // Only translate the exception message so we don't have to serialize the whole object
            _error = exception.Message;
        }

        /// <summary>
        /// Gets or sets the <see cref="ElementLocation"/> of the reference that created this response.
        /// </summary>
        public Construction.ElementLocation ElementLocation { get; set; }

        /// <summary>
        /// Gets an optional error associated with the response.
        /// </summary>
        public string Error => _error;

        /// <summary>
        /// Gets the full path to the resolved SDK.
        /// </summary>
        public string FullPath => _fullPath;

        public NodePacketType Type => NodePacketType.ResolveSdkResponse;

        /// <summary>
        /// Gets the version of the resolved SDK.
        /// </summary>
        public string Version => _version;

        public static INodePacket FactoryForDeserialization(INodePacketTranslator translator)
        {
            return new SdkResolverResponse(translator);
        }

        public void Translate(INodePacketTranslator translator)
        {
            translator.Translate(ref _fullPath);
            translator.Translate(ref _version);
            translator.Translate(ref _error);
        }
    }
}
