// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

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

        private static ImmutableDictionary<Key, string> _fullNameByKey = ImmutableDictionary<Key, string>.Empty;

        private string? _fullName;

        /// <summary>
        /// Constructor
        /// </summary>
        internal SolutionConfigurationInSolution(string configurationName, string platformName)
        {
            ConfigurationName = configurationName;
            PlatformName = platformName;
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
        public string FullName => _fullName ??= ComputeFullName(ConfigurationName, PlatformName);

        /// <summary>
        /// Given a configuration name and a platform name, compute the full name
        /// of this configuration.
        /// </summary>
        internal static string ComputeFullName(string configurationName, string platformName)
        {
            // Some configurations don't have the platform part
            if (string.IsNullOrEmpty(platformName))
            {
                return configurationName;
            }

            return ImmutableInterlocked.GetOrAdd(
                ref _fullNameByKey,
                new Key(configurationName, platformName),
                static key => $"{key.Configuration}|{key.Platform}");
        }

        private record struct Key(string Configuration, string Platform);
    }
}
