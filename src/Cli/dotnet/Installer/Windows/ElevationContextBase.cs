// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Versioning;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Encapsulates information to manage process elevation.
    /// </summary>
#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    internal abstract class ElevationContextBase
    {
        /// <summary>
        /// Gets whether the current process has start a second, elevated copy of the host.
        /// </summary>
        public bool HasElevated
        {
            get;
            protected set;
        }

        /// <summary>
        /// <see langword="true"/> if the the current user has elevated permissions.
        /// </summary>
        public abstract bool IsElevated
        {
            get;
        }

        /// <summary>
        /// Starts a new process with elevated privileges.
        /// </summary>
        public abstract void Elevate();
    }
}
