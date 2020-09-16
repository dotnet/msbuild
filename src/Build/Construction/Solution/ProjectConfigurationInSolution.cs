// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// This class represents an entry for a project configuration in a solution configuration.
    /// </summary>
    public sealed class ProjectConfigurationInSolution
    {
        /// <summary>
        /// Constructor
        /// </summary>
        internal ProjectConfigurationInSolution(string configurationName, string platformName, bool includeInBuild)
        {
            ConfigurationName = configurationName;
            PlatformName = RemoveSpaceFromAnyCpuPlatform(platformName);
            IncludeInBuild = includeInBuild;
            FullName = SolutionConfigurationInSolution.ComputeFullName(ConfigurationName, PlatformName);
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
        /// True if this project configuration should be built as part of its parent solution configuration
        /// </summary>
        public bool IncludeInBuild { get; }

        /// <summary>
        /// This is a hacky method to remove the space in the "Any CPU" platform in project configurations.
        /// The problem is that this platform is stored as "AnyCPU" in project files, but the project system
        /// reports it as "Any CPU" to the solution configuration manager. Because of that all solution configurations
        /// contain the version with a space in it, and when we try and give that name to actual projects, 
        /// they have no clue what we're talking about. We need to remove the space in project platforms so that
        /// the platform name matches the one used in projects.
        /// </summary>
        private static string RemoveSpaceFromAnyCpuPlatform(string platformName)
        {
            if (string.Equals(platformName, "Any CPU", StringComparison.OrdinalIgnoreCase))
            {
                return "AnyCPU";
            }

            return platformName;
        }
    }
}
