// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Describes different installation contexts.
    /// </summary>
    /// <remarks>
    /// See https://docs.microsoft.com/en-us/windows/win32/msi/determining-installation-context
    /// </remarks>
    [Flags]
    public enum MsiInstallContext : int
    {
        /// <summary>
        /// The product visible to the current user.
        /// </summary>
        FIRSTVISIBLE = 0,

        /// <summary>
        /// Invalid context for a product.
        /// </summary>
        NONE = 0,

        /// <summary>
        /// User managed install context.
        /// </summary>
        USERMANAGED = 1,

        /// <summary>
        /// User non-managed context 
        /// </summary>
        USERUNMANAGED = 2,

        /// <summary>
        /// Per-machine context
        /// </summary>
        MACHINE = 4,

        /// <summary>
        /// All contexts.
        /// </summary>
        ALL = (USERMANAGED | USERUNMANAGED | MACHINE),

        /// <summary>
        /// All user-managed contexts.
        /// </summary>
        ALLUSERMANAGED = 8,
    }
}
