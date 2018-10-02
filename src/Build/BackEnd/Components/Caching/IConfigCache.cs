// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Delegate invoked to create a configuration
    /// </summary>
    internal delegate BuildRequestConfiguration ConfigCreateCallback(BuildRequestConfiguration existingConfiguration, bool loadProject);

    /// <summary>
    /// An interfacing representing a build request configuration cache.
    /// </summary>
    internal interface IConfigCache : IBuildComponent, IEnumerable<BuildRequestConfiguration>
    {
        /// <summary>
        /// Returns the configuration with the specified id.
        /// </summary>
        /// <param name="configId">The configuration id.</param>
        /// <returns>The configuration with the specified id.</returns>
        BuildRequestConfiguration this[int configId]
        {
            get;
        }

        /// <summary>
        /// Adds the configuration to the cache.
        /// </summary>
        /// <param name="config">The configuration to add.</param>
        void AddConfiguration(BuildRequestConfiguration config);

        /// <summary>
        /// Removes the specified configuration from the cache.
        /// </summary>
        /// <param name="configId">The id of the configuration to remove.</param>
        void RemoveConfiguration(int configId);

        /// <summary>
        /// Gets the cached configuration which matches the specified configuration
        /// </summary>
        /// <param name="config">The configuration to match.</param>
        /// <returns>The matching configuration if any, null otherwise.</returns>
        BuildRequestConfiguration GetMatchingConfiguration(BuildRequestConfiguration config);

        /// <summary>
        /// Gets the cached configuration which matches the specified configuration
        /// </summary>
        /// <param name="configMetadata">The configuration metadata to match.</param>
        /// <returns>The matching configuration if any, null otherwise.</returns>
        BuildRequestConfiguration GetMatchingConfiguration(ConfigurationMetadata configMetadata);

        /// <summary>
        /// Gets a matching configuration.  If no such configration exists, one is created and optionally loaded.
        /// </summary>
        /// <param name="configMetadata">The configuration metadata to match.</param>
        /// <param name="callback">Callback to be invoked if the configuration does not exist.</param>
        /// <param name="loadProject">True if the configuration should also be loaded.</param>
        /// <returns>The matching configuration if any, null otherwise.</returns>
        BuildRequestConfiguration GetMatchingConfiguration(ConfigurationMetadata configMetadata, ConfigCreateCallback callback, bool loadProject);

        /// <summary>
        /// Returns true if a configuration with the specified id exists in the cache.
        /// </summary>
        /// <param name="configId">The configuration id to check.</param>
        /// <returns>
        /// True if there is a configuration with the specified id, false otherwise.
        /// </returns>
        bool HasConfiguration(int configId);

        /// <summary>
        /// Clears out the configurations
        /// </summary>
        void ClearConfigurations();

        /// <summary>
        /// Clear non explicltly loaded configurations. 
        /// </summary>
        /// <returns>The configuration ids which have been cleared.</returns>
        List<int> ClearNonExplicitlyLoadedConfigurations();

        /// <summary>
        /// Check whether the config cache has more items that the predefined threshold
        /// </summary>
        bool IsConfigCacheSizeLargerThanThreshold();

        /// <summary>
        /// Unloads any configurations not in use.
        /// </summary>
        /// <returns>True if any configurations were cached, false otherwise.</returns>
        bool WriteConfigurationsToDisk();
    }
}
