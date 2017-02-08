// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Represents a project configuration (e.g. "Debug|x86")</summary>
//-----------------------------------------------------------------------

using System;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// This class represents an entry for a project configuration in a solution configuration.
    /// </summary>
    public sealed class ProjectConfigurationInSolution
    {
        /// <summary>
        /// The configuration part of this configuration - e.g. "Debug", "Release"
        /// </summary>
        private string _configurationName;

        /// <summary>
        /// The platform part of this configuration - e.g. "Any CPU", "Win32"
        /// </summary>
        private string _platformName;

        /// <summary>
        /// The full name of this configuration - e.g. "Debug|Any CPU"
        /// </summary>
        private string _fullName;

        /// <summary>
        /// True if this project configuration should be built as part of its parent solution configuration
        /// </summary>
        private bool _includeInBuild;

        /// <summary>
        /// Constructor
        /// </summary>
        internal ProjectConfigurationInSolution(string configurationName, string platformName, bool includeInBuild)
        {
            _configurationName = configurationName;
            _platformName = RemoveSpaceFromAnyCpuPlatform(platformName);
            _includeInBuild = includeInBuild;
            _fullName = SolutionConfigurationInSolution.ComputeFullName(_configurationName, _platformName);
        }

        /// <summary>
        /// The configuration part of this configuration - e.g. "Debug", "Release"
        /// </summary>
        public string ConfigurationName
        {
            get { return _configurationName; }
        }

        /// <summary>
        /// The platform part of this configuration - e.g. "Any CPU", "Win32"
        /// </summary>
        public string PlatformName
        {
            get { return _platformName; }
        }

        /// <summary>
        /// The full name of this configuration - e.g. "Debug|Any CPU"
        /// </summary>
        public string FullName
        {
            get { return _fullName; }
        }

        /// <summary>
        /// True if this project configuration should be built as part of its parent solution configuration
        /// </summary>
        public bool IncludeInBuild
        {
            get { return _includeInBuild; }
        }

        /// <summary>
        /// This is a hacky method to remove the space in the "Any CPU" platform in project configurations.
        /// The problem is that this platform is stored as "AnyCPU" in project files, but the project system
        /// reports it as "Any CPU" to the solution configuration manager. Because of that all solution configurations
        /// contain the version with a space in it, and when we try and give that name to actual projects, 
        /// they have no clue what we're talking about. We need to remove the space in project platforms so that
        /// the platform name matches the one used in projects.
        /// </summary>
        static private string RemoveSpaceFromAnyCpuPlatform(string platformName)
        {
            if (string.Compare(platformName, "Any CPU", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "AnyCPU";
            }

            return platformName;
        }
    }
}
