// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Win32.Msi;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Common install properties that can be passed to MSIs. 
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class InstallProperties
    {
        /// <summary>
        /// Set MSIFASTINSTALL=7 and be explicit about what this means.
        /// </summary>
        private const int _msiFastInstall = (int)(MsiFastInstall.NoSystemRestore | MsiFastInstall.OnlyFileCosting | MsiFastInstall.ReducedProgressFrequency);

        /// <summary>
        /// Hides MSI entries from Add/Remove Programs by setting ARPSYSTEMCOMPONENT.
        /// </summary>
        public static readonly string SystemComponent = $"ARPSYSTEMCOMPONENT=1";

        /// <summary>
        /// Sets IGNOREDEPENDENCIES=ALL, allowing the MSI to be removed when it
        /// still contains dependents.
        /// </summary>
        public static readonly string IgnoreDependencies = $"IGNOREDEPENDENCIES=ALL";

        /// <summary>
        /// Reduce the number of actions taken during the InstallValidate action.
        /// </summary>
        public static readonly string FastInstall = $"MSIFASTINSTALL={_msiFastInstall}";

        /// <summary>
        /// Remove all features and components of an installed product.
        /// </summary>
        public static readonly string RemoveAll = $"REMOVE=ALL";

        /// <summary>
        /// Suppress all restarts and restart prompts initiated by ForceReboot during the installation. 
        /// Suppress all restarts and restart prompts at the end of the installation. Both the restart 
        /// prompt and the restart itself are suppressed.
        /// </summary>
        public static readonly string SuppressReboot = $"REBOOT=ReallySuppress";

        /// <summary>
        /// Creates a single string containing installer properties based on a set of individual properties.
        /// </summary>
        /// <param name="properties">The individual properties, each expressed as &lt;property&gt;=&lt;value&gt;. Public property names
        /// must be all uppercase, e.g. MYPULICPROPERTY=3.</param>
        /// <returns>A string containing all the properties or <see langword="null" /> if <paramref name="properties"/> contain no values.</returns>
        internal static string Create(params string[] properties)
        {
            string[] props = properties.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

            return props.Length > 0 ? string.Join(' ', props) : null;
        }
    }
}
