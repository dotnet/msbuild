//-----------------------------------------------------------------------
// <copyright file="BuildRequestConfigurationTestExtension.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension for the BuildRequestConfiguration implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Build.BackEnd;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Test extension for BuildRequestConfigurationTestExtension implementation.
    /// </summary>
    public class BuildRequestConfigurationTestExtension : TestExtension<BuildRequestConfigurationVerifier>
    {
        /// <summary>
        /// Initializes a new instance of the BuildRequestConfigurationTestExtension class.
        /// </summary>
        /// <param name="configuration">Configuration entry from the cache.</param>
        internal BuildRequestConfigurationTestExtension(BuildRequestConfiguration configuration)
            : base()
        {
            this.Configuration = configuration;
        }

        /// <summary>
        /// Gets a value indicating the name of the cache file for this configuration.
        /// </summary>
        public string CacheFileName
        {
            get
            {
                return this.Configuration.GetCacheFile();
            }
        }

        /// <summary>
        /// Gets the configuration id.
        /// </summary>
        public int ConfigurationId
        {
            get
            {
                return this.Configuration.ConfigurationId;
            }
        }

        /// <summary>
        /// Gets BuildRequestConfiguration object from the cache.
        /// </summary>
        internal BuildRequestConfiguration Configuration
        {
            get;
            private set;
        }
    }
}
