// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// The state associated with a Windows Installer package after
    /// attempting to detect it.
    /// </summary>
    internal enum DetectState
    {
        /// <summary>
        /// The installation state of the package could not be determined.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// The package is already installed.
        /// </summary>
        Present = 1,
        /// <summary>
        /// The package is not installed.
        /// </summary>
        Absent = 2,
        /// <summary>
        /// A newer version of the package is already installed.
        /// </summary>
        Superseded = 3,
        /// <summary>
        /// The installed package is older than the new package.
        /// </summary>
        Obsolete = 4,
    }
}
