// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Values for the MSIFASTINSTALL property to reduce the time required to install large Windows Installer packages. The property
    /// may be set on the command line or in the Property table. 
    /// </summary>
    /// <remarks>
    /// The property is only available in Windows Installer 5.0 or later.
    /// </remarks>
    [Flags]
    public enum MsiFastInstall : int
    {
        /// <summary>
        /// No operations are skipped.
        /// </summary>
        Default = 0,

        /// <summary>
        /// No system restore point is saved for the installation.
        /// </summary>
        NoSystemRestore = 1,

        /// <summary>
        /// Perform only file costing and skip checking other costs.
        /// </summary>
        OnlyFileCosting = 2,

        /// <summary>
        /// Reduce the frequency of progress messages.
        /// </summary>
        ReducedProgressFrequency = 4
    }
}
