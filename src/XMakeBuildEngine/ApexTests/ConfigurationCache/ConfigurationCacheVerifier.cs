//-----------------------------------------------------------------------
// <copyright file="ConfigurationCacheVerifier.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension verifier for the ConfigCache implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Test extension verifier for the ConfigCache implementation.
    /// </summary>
    public class ConfigurationCacheVerifier : TestExtensionVerifier<ConfigurationCacheTestExtension>
    {
        /// <summary>
        /// Gets Test extension associated with this verifier.
        /// </summary>
        internal new ConfigurationCacheTestExtension TestExtension
        {
            get
            {
                return base.TestExtension as ConfigurationCacheTestExtension;
            }
        }

        /// <summary>
        /// Verifies if a configuration for the build request data exists in the cache.
        /// </summary>
        /// <param name="requestData">BuildRequestData used to create the original configuration.</param>
        /// <param name="toolsVersion">Tools version of the configuration.</param>
        public void CacheContainsConfigurationForBuildRequest(BuildRequestData requestData, string toolsVersion)
        {
            BuildRequestConfigurationTestExtension configurationTestExtension = this.TestExtension.GetConfigurationFromCache(requestData, toolsVersion);
            this.Verifier.IsNotNull(configurationTestExtension, "Configuration should exist in the cache.");
            this.Verifier.IsTrue(String.Compare(configurationTestExtension.Configuration.ToolsVersion, toolsVersion, StringComparison.OrdinalIgnoreCase) == 0, "Configuration tools version should match the passed in tools version.");
        }
    }
}