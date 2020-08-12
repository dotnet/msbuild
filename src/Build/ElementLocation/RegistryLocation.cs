// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using System;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// The location something in the registry
    /// </summary>
    /// <remarks>
    /// This object is IMMUTABLE, so that it can be passed around arbitrarily.
    /// This is not public because the current implementation only provides correct data for unedited projects.
    /// DO NOT make it public without considering a solution to this problem.
    /// </remarks>
    [Serializable]
    internal class RegistryLocation : IElementLocation, ITranslatable
    {
        /// <summary>
        /// The location.
        /// </summary>
        private string registryPath;

        /// <summary>
        /// Constructor taking the registry location.
        /// </summary>
        internal RegistryLocation(string registryPath)
        {
            ErrorUtilities.VerifyThrowInternalLength(registryPath, nameof(registryPath));

            this.registryPath = registryPath;
        }

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private RegistryLocation(ITranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Not relevant, returns empty string.
        /// </summary>
        public string File
        {
            get { return String.Empty; }
        }

        /// <summary>
        /// Not relevant, returns 0.
        /// </summary>
        public int Line
        {
            get { return 0; }
        }

        /// <summary>
        /// Not relevant, returns 0.
        /// </summary>
        public int Column
        {
            get { return 0; }
        }

        /// <summary>
        /// The location in a form suitable for replacement
        /// into a message.
        /// </summary>
        public string LocationString
        {
            get { return registryPath; }
        }

        #region INodePacketTranslatable Members

        /// <summary>
        /// Reads or writes the packet to the serializer.
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref registryPath);
        }

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        static internal RegistryLocation FactoryForDeserialization(ITranslator translator)
        {
            return new RegistryLocation(translator);
        }

        #endregion
    }
}
