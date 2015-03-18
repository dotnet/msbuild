// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Represents a solution configuration (e.g. "Debug|x86")</summary>
//-----------------------------------------------------------------------

using System;
using System.Globalization;

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
        /// Constructor
        /// </summary>
        internal SolutionConfigurationInSolution(string configurationName, string platformName)
        {
            _configurationName = configurationName;
            _platformName = platformName;
            _fullName = ComputeFullName(configurationName, platformName);
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
        /// Given a configuration name and a platform name, compute the full name 
        /// of this configuration
        /// </summary>
        internal static string ComputeFullName(string configurationName, string platformName)
        {
            // Some configurations don't have the platform part
            if ((platformName != null) && (platformName.Length > 0))
            {
                return String.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", configurationName, ConfigurationPlatformSeparator, platformName);
            }
            else
            {
                return configurationName;
            }
        }
    }
}
