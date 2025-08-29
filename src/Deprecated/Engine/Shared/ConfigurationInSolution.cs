// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System.Globalization;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This represents basic configuration functionality used in solution and project configurations.
    /// Since solution configurations don't need anything else, they are represented with this class.
    /// </summary>
    /// <owner>LukaszG</owner>
    internal class ConfigurationInSolution
    {
        internal const char configurationPlatformSeparator = '|';

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configurationName"></param>
        /// <param name="platformName"></param>
        /// <owner>LukaszG</owner>
        internal ConfigurationInSolution(string configurationName, string platformName)
        {
            this.configurationName = configurationName;
            this.platformName = platformName;

            // Some configurations don't have the platform part
            if (!string.IsNullOrEmpty(platformName))
            {
                this.fullName = string.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", configurationName, configurationPlatformSeparator, platformName);
            }
            else
            {
                this.fullName = configurationName;
            }
        }

        private string configurationName;

        /// <summary>
        /// The configuration part of this, uh, configuration - e.g. "Debug", "Release"
        /// </summary>
        /// <owner>LukaszG</owner>
        internal string ConfigurationName
        {
            get { return this.configurationName; }
        }

        private string platformName;

        /// <summary>
        /// The platform part of this configuration - e.g. "Any CPU", "Win32"
        /// </summary>
        /// <owner>LukaszG</owner>
        internal string PlatformName
        {
            get { return this.platformName; }
        }

        private string fullName;

        /// <summary>
        /// The full name of this configuration - e.g. "Debug|Any CPU"
        /// </summary>
        /// <owner>LukaszG</owner>
        internal string FullName
        {
            get { return this.fullName; }
        }

        private BuildItemGroup projectBuildItems;

        /// <summary>
        /// Build items corresponding to projects built in this configuration
        /// </summary>
        internal BuildItemGroup ProjectBuildItems
        {
            get { return this.projectBuildItems; }
            set { this.projectBuildItems = value; }
        }
    }
}
