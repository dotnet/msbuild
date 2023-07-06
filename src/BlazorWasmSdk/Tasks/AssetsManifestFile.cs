// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
#pragma warning disable IDE1006 // Naming Styles
    public class AssetsManifestFile
    {
        /// <summary>
        /// Gets or sets a version string.
        /// </summary>
        public string version { get; set; }

        /// <summary>
        /// Gets or sets the assets. Keys are URLs; values are base-64-formatted SHA256 content hashes.
        /// </summary>
        public AssetsManifestFileEntry[] assets { get; set; }
    }

    public class AssetsManifestFileEntry
    {
        /// <summary>
        /// Gets or sets the asset URL. Normally this will be relative to the application's base href.
        /// </summary>
        public string url { get; set; }

        /// <summary>
        /// Gets or sets the file content hash. This should be the base-64-formatted SHA256 value.
        /// </summary>
        public string hash { get; set; }
    }
#pragma warning restore IDE1006 // Naming Styles
}
