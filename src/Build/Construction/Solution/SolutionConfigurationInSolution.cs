// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// This represents an entry for a solution configuration
    /// </summary>
    public sealed class SolutionConfigurationInSolution
    {
        /// <summary>
        /// Default separator between configuration and platform in configuration
        /// full names
        /// </summary>
        internal const char ConfigurationPlatformSeparator = '|';

        internal static readonly char[] ConfigurationPlatformSeparatorArray = { '|' };

        /// <summary>
        /// Constructor
        /// </summary>
        internal SolutionConfigurationInSolution(string configurationName, string platformName)
        {
            ConfigurationName = configurationName;
            PlatformName = platformName;
            FullName = ComputeFullName(configurationName, platformName);
        }

        /// <summary>
        /// The configuration part of this configuration - e.g. "Debug", "Release"
        /// </summary>
        public string ConfigurationName { get; }

        /// <summary>
        /// The platform part of this configuration - e.g. "Any CPU", "Win32"
        /// </summary>
        public string PlatformName { get; }

        /// <summary>
        /// The full name of this configuration - e.g. "Debug|Any CPU"
        /// </summary>
        public string FullName { get; }

        /// <summary>
        /// Given a configuration name and a platform name, compute the full name
        /// of this configuration
        /// </summary>
        internal static string ComputeFullName(string configurationName, string platformName)
        {
            // Some configurations don't have the platform part
            if (!string.IsNullOrEmpty(platformName))
            {
                return $"{configurationName}{ConfigurationPlatformSeparator}{platformName}";
            }
            return configurationName;
        }
    }
}
