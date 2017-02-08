//-----------------------------------------------------------------------
// <copyright file="ConfigurationCacheTestExtension.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension for the ConfigCache implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Build.BackEnd;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Test extension for ConfigCache implementation.
    /// </summary>
    public class ConfigurationCacheTestExtension : TestExtension<ConfigurationCacheVerifier>
    {
        /// <summary>
        /// Initializes a new instance of the ConfigurationCacheTestExtension class.
        /// </summary>
        /// <param name="configCache">ConfigurationCache component instance.</param>
        internal ConfigurationCacheTestExtension(ConfigCache configCache)
            : base()
        {
            this.ConfigCache = configCache;
        }

        /// <summary>
        /// Gets BuildManager component for MSBuild. BuildManager should not be exposed publicly.
        /// </summary>
        internal ConfigCache ConfigCache
        {
            get;
            private set;
        }

        /// <summary>
        /// Writes project configurations to disk.
        /// </summary>
        public void WriteConfigurationsToDisk()
        {
            this.ConfigCache.WriteConfigurationsToDisk();
        }

        /// <summary>
        /// Retrieves the configuration cache from the data provided.
        /// </summary>
        /// <param name="requestData">Data containing the build request entry.</param>
        /// <param name="toolsVersion">Tools version used to build the request.</param>
        /// <returns>BuildRequestConfigurationTestExtension which contains the configuration retrieved from the cache. Can return NULL if the entry for the data is not in the cache.</returns>
        public BuildRequestConfigurationTestExtension GetConfigurationFromCache(BuildRequestData requestData, string toolsVersion)
        {
            BuildRequestConfiguration unresolvedConfiguration = new BuildRequestConfiguration(requestData, toolsVersion);
            BuildRequestConfiguration resolvedConfiguration = this.ConfigCache.GetMatchingConfiguration(unresolvedConfiguration);
            if (resolvedConfiguration == null)
            {
                return null;
            }

            return TestExtensionHelper.Create<BuildRequestConfigurationTestExtension, BuildRequestConfiguration>(resolvedConfiguration, this);
        }
    }
}