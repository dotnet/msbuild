// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// Represents an SDK resolver response sent between nodes.  This is mostly a wrapper around an <see cref="SdkResult"/>
    /// with an additional <see cref="INodePacket"/> implementation.
    /// </summary>
    internal sealed class SdkResolverResponse : INodePacket
    {
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

        /// <summary>
        /// Gets or sets the <see cref="ElementLocation"/> of the reference that created this response.
        /// </summary>
        public Construction.ElementLocation ElementLocation { get; set; }

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
        }
    }
}
