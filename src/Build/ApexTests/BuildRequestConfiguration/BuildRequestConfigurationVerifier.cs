//-----------------------------------------------------------------------
// <copyright file="BuildRequestConfigurationVerifier.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension verifier for the BuildRequestConfiguration implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.Build.BackEnd;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Test extension verifier for the BuildRequestConfigurationVerifier implementation.
    /// </summary>
    public class BuildRequestConfigurationVerifier : TestExtensionVerifier<BuildRequestConfigurationTestExtension>
    {
        /// <summary>
        /// Gets Test extension associated with this verifier.
        /// </summary>
        internal new BuildRequestConfigurationTestExtension TestExtension
        {
            get
            {
                return base.TestExtension as BuildRequestConfigurationTestExtension;
            }
        }

        /// <summary>
        /// Verifies if the configuration is set to cacheable.
        /// </summary>
        public void ConfigurationIsCacheable()
        {
            this.Verifier.IsTrue(this.TestExtension.Configuration.IsCacheable, "Configuration should be cacheable.");
        }

        /// <summary>
        /// Verifies if the configuration is loaded.
        /// </summary>
        public void ConfigurationIsLoaded()
        {
            this.Verifier.IsTrue(this.TestExtension.Configuration.IsLoaded, "Configuration should be loaded.");
        }

        /// <summary>
        /// Verifies if the configuration is not loaded.
        /// </summary>
        public void ConfigurationIsUnloaded()
        {
            this.Verifier.IsFalse(this.TestExtension.Configuration.IsLoaded, "Configuration should not be loaded.");
        }

        /// <summary>
        /// Verifies if the configuration is cached.
        /// </summary>
        public void ConfigurationIsCached()
        {
            this.Verifier.IsTrue(this.TestExtension.Configuration.IsCached, "Configuration should be cached.");
        }

        /// <summary>
        /// Verifies if the configuration is not cached.
        /// </summary>
        public void ConfigurationIsNotCached()
        {
            this.Verifier.IsFalse(this.TestExtension.Configuration.IsCached, "Configuration should not be cached.");
        }

        /// <summary>
        /// Verifies if the configuration cache file has been created in disk.
        /// </summary>
        public void ConfigurationCacheFileExists()
        {
            this.Verifier.IsTrue(File.Exists(this.TestExtension.CacheFileName), "Cache filename {0} does not exist.", this.TestExtension.CacheFileName);
        }

        /// <summary>
        /// Verifies if the configuration cache file has not been created in disk.
        /// </summary>
        public void ConfigurationCacheFileDoesNotExists()
        {
            this.Verifier.IsFalse(File.Exists(this.TestExtension.CacheFileName), "Cache filename {0} does not exist.", this.TestExtension.CacheFileName);
        }
    }
}