// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System.IO;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Represents the payload associated with a single workload pack installer. The payload
    /// consists of the installer (MSI) and its JSON manifest.
    /// </summary>
    internal class MsiPayload
    {
        private MsiManifest _manifest;

        /// <summary>
        /// The full path of the JSON manifest associated with this payload.
        /// </summary>
        public readonly string ManifestPath;

        /// <summary>
        /// The full path of the MSI associated with this payload.
        /// </summary>
        public readonly string MsiPath;

        /// <summary>
        /// The manifest data describing the associated MSI.
        /// </summary>
        public MsiManifest Manifest
        {
            get
            {
                if (_manifest == null)
                {
                    _manifest = JsonConvert.DeserializeObject<MsiManifest>(File.ReadAllText(ManifestPath));
                }

                return _manifest;
            }
        }

        public MsiPayload(string manifestPath, string msiPath)
        {
            ManifestPath = manifestPath;
            MsiPath = msiPath;
        }
    }
}
