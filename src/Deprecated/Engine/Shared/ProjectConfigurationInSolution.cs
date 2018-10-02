// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Text;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class represents an entry for a project configuration in a solution configuration.
    /// </summary>
    /// <owner>LukaszG</owner>
    internal class ProjectConfigurationInSolution : ConfigurationInSolution
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configurationName"></param>
        /// <param name="platformName"></param>
        /// <param name="includeInBuild"></param>
        /// <owner>LukaszG</owner>
        internal ProjectConfigurationInSolution(string configurationName, string platformName, bool includeInBuild)
            : base(configurationName, RemoveSpaceFromAnyCpuPlatform(platformName))
        {
            this.includeInBuild = includeInBuild;
        }

        private bool includeInBuild;

        /// <summary>
        /// true if this project configuration should be built as part of its parent solution configuration
        /// </summary>
        /// <owner>LukaszG</owner>
        internal bool IncludeInBuild
        {
            get { return this.includeInBuild; }
        }

        /// <summary>
        /// This is a hacky method to remove the space in the "Any CPU" platform in project configurations.
        /// The problem is that this platform is stored as "AnyCPU" in project files, but the project system
        /// reports it as "Any CPU" to the solution configuration manager. Because of that all solution configurations
        /// contain the version with a space in it, and when we try and give that name to actual projects, 
        /// they have no clue what we're talking about. We need to remove the space in project platforms so that
        /// the platform name matches the one used in projects.
        /// </summary>
        /// <param name="platformName"></param>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        static private string RemoveSpaceFromAnyCpuPlatform(string platformName)
        {
            if (string.Compare(platformName, "Any CPU", StringComparison.OrdinalIgnoreCase) == 0)
                return "AnyCPU";

            return platformName;
        }
    }
}
