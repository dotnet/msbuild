// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Specifies the installation state of a product, feature or component.
    /// </summary>
    public enum InstallState
    {
        /// <summary>
        /// The component is disabled.
        /// </summary>
        NOTUSED = -7,

        /// <summary>
        /// The installation configuration data is corrupt.
        /// </summary>
        BADCONFIG = -6,

        /// <summary>
        /// The installation is suspended or in progress.
        /// </summary>
        INCOMPLETE = -5,

        /// <summary>
        /// The feature must run from the source, and the source is unavailable.
        /// </summary>
        SOURCEABSENT = -4,

        /// <summary>
        /// The return buffer is full.
        /// </summary>
        MOREDATA = -3,

        /// <summary>
        /// An invalid parameter was passed to the function.
        /// </summary>
        INVALIDARG = -2,

        /// <summary>
        /// An unrecognized product or feature name was passed to the function.
        /// </summary>
        UNKNOWN = -1,

        /// <summary>
        /// The feature is broken.
        /// </summary>
        BROKEN = 0,

        /// <summary>
        /// The advertised feature.
        /// </summary>
        ADVERTISED = 1,

        /// <summary>
        /// The component is being removed. In action state and not settable.
        /// </summary>
        REMOVED = 1,

        /// <summary>
        /// The component or feature was uninstalled.
        /// </summary>
        ABSENT = 2,

        /// <summary>
        /// The component or feature is installed on the local drive.
        /// </summary>
        LOCAL = 3,

        /// <summary>
        /// The component runs from the source, CD-ROM, or network.
        /// </summary>
        SOURCE = 4,

        /// <summary>
        /// The component is installed in the default location: local or source.
        /// </summary>
        DEFAULT = 5
    }
}
