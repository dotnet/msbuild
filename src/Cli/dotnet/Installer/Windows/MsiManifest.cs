// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Defines MSI properties stored in the JSON manifest of a workload pack.
    /// </summary>
    internal class MsiManifest
    {
        /// <summary>
        /// The number of bytes the MSI requires to be installed.
        /// </summary>
        public long InstallSize
        {
            get;
            set;
        }

        /// <summary>
        /// The name of the MSI package.
        /// </summary>
        public string Payload
        {
            get;
            set;
        }

        /// <summary>
        /// The product code of the MSI.
        /// </summary>
        public string ProductCode
        {
            get;
            set;
        }

        /// <summary>
        /// The product version of the MSI.
        /// </summary>
        public Version ProductVersion
        {
            get;
            set;
        }

        /// <summary>
        /// The name of the dependency provider key used to track installation reference counts.
        /// </summary>
        public string ProviderKeyName
        {
            get;
            set;
        }

        /// <summary>
        /// The upgrade code of the MSI.
        /// </summary>
        public string UpgradeCode
        {
            get;
            set;
        }
    }
}
