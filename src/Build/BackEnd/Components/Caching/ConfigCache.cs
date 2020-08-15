// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Implements a build request configuration cache.
    /// </summary>
    internal class ConfigCache : IConfigCache
    {
        /// <summary>
        /// The configurations
        /// </summary>
        private IDictionary<int, BuildRequestConfiguration> _configurations;

        /// <summary>
        /// Object used for locking.
        /// </summary>
        private object _lockObject = new object();

        /// <summary>
        /// Lookup which can be used to find a configuration with the specified metadata.
        /// </summary>
        private IDictionary<ConfigurationMetadata, int> _configurationIdsByMetadata;

        /// <summary>
        /// The maximum cache entries allowed before a sweep can occur.
        /// </summary>
        private int _sweepThreshhold;

        /// <summary>
        /// Creates a new build configuration cache.
        /// </summary>
        public ConfigCache()
        {
            _configurations = new Dictionary<int, BuildRequestConfiguration>();
            _configurationIdsByMetadata = new Dictionary<ConfigurationMetadata, int>();
            if (!int.TryParse(Environment.GetEnvironmentVariable("MSBUILDCONFIGCACHESWEEPTHRESHHOLD"), out _sweepThreshhold))
            {
                _sweepThreshhold = 500;
            }
        }

        /// <summary>
        /// Returns the configuration cached under the specified configuration id.
        /// </summary>
        /// <param name="configId">The id of the configuration to return.</param>
        /// <returns>The cached configuration.</returns>
        /// <exception cref="KeyNotFoundException">Returned if a configuration with the specified id is not in the cache.</exception>
        public BuildRequestConfiguration this[int configId]
        {
            get
            {
                lock (_lockObject)
                {
                    return _configurations[configId];
                }
            }
        }

        #region IConfigCache Members

        /// <summary>
        /// Adds the specified configuration to the cache.
        /// </summary>
        /// <param name="config">The configuration to add.</param>
        public void AddConfiguration(BuildRequestConfiguration config)
        {
            ErrorUtilities.VerifyThrowArgumentNull(config, nameof(config));
            ErrorUtilities.VerifyThrow(config.ConfigurationId != 0, "Invalid configuration ID");

            lock (_lockObject)
            {
                int configId = GetKeyForConfiguration(config);
                ErrorUtilities.VerifyThrow(!_configurations.ContainsKey(configId), "Configuration {0} already cached", config.ConfigurationId);
                _configurations.Add(configId, config);
                _configurationIdsByMetadata.Add(new ConfigurationMetadata(config), configId);
            }
        }

        /// <summary>
        /// Removes the specified configuration from the cache.
        /// </summary>
        /// <param name="configId">The id of the configuration to remove.</param>
        public void RemoveConfiguration(int configId)
        {
            lock (_lockObject)
            {
                BuildRequestConfiguration config = _configurations[configId];
                _configurations.Remove(configId);
                _configurationIdsByMetadata.Remove(new ConfigurationMetadata(config));
                config.ClearCacheFile();
            }
        }

        /// <summary>
        /// Returns the entry in the cache which matches the specified config.
        /// </summary>
        /// <param name="config">The configuration to match</param>
        /// <returns>A matching configuration if one exists, null otherwise.</returns>
        public BuildRequestConfiguration GetMatchingConfiguration(BuildRequestConfiguration config)
        {
            ErrorUtilities.VerifyThrowArgumentNull(config, nameof(config));
            return GetMatchingConfiguration(new ConfigurationMetadata(config));
        }

        /// <summary>
        /// Returns the entry in the cache which matches the specified config.
        /// </summary>
        /// <param name="configMetadata">The configuration metadata to match</param>
        /// <returns>A matching configuration if one exists, null otherwise.</returns>
        public BuildRequestConfiguration GetMatchingConfiguration(ConfigurationMetadata configMetadata)
        {
            ErrorUtilities.VerifyThrowArgumentNull(configMetadata, nameof(configMetadata));
            lock (_lockObject)
            {
                int configId;
                if (!_configurationIdsByMetadata.TryGetValue(configMetadata, out configId))
                {
                    return null;
                }

                return _configurations[configId];
            }
        }

        /// <summary>
        /// Gets a matching configuration.  If no such configuration exists, one is created and optionally loaded.
        /// </summary>
        public BuildRequestConfiguration GetMatchingConfiguration(ConfigurationMetadata configMetadata, ConfigCreateCallback callback, bool loadProject)
        {
            lock (_lockObject)
            {
                BuildRequestConfiguration configuration = GetMatchingConfiguration(configMetadata);

                // If there is no matching configuration, let the caller create one.
                if (configuration == null)
                {
                    configuration = callback(null, loadProject);
                    AddConfiguration(configuration);
                }
                else if (loadProject)
                {
                    // We already had a configuration, load the project
                    // If it exists but it cached, retrieve it 
                    if (configuration.IsCached)
                    {
                        configuration.RetrieveFromCache();
                    }

                    // If it is still not loaded (because no instance was ever created here), let the caller populate the instance.
                    if (!configuration.IsLoaded)
                    {
                        callback(configuration, loadProject: true);
                    }
                }

                // In either case, make sure the project is loaded if it was requested.
                if (loadProject)
                {
                    ErrorUtilities.VerifyThrow(configuration.IsLoaded, "Request to create configuration did not honor request to also load project.");
                }

                return configuration;
            }
        }

        /// <summary>
        /// Returns true if the cache contains a configuration with the specified id, false otherwise.
        /// </summary>
        /// <param name="configId">The configuration id to check.</param>
        /// <returns>True if the cache contains a configuration with this id, false otherwise.</returns>
        public bool HasConfiguration(int configId)
        {
            lock (_lockObject)
            {
                return _configurations.ContainsKey(configId);
            }
        }

        /// <summary>
        /// Clear all configurations
        /// </summary>
        public void ClearConfigurations()
        {
            lock (_lockObject)
            {
                foreach (var config in _configurations.Values)
                {
                    config.ClearCacheFile();
                }

                _configurations = new Dictionary<int, BuildRequestConfiguration>();
                _configurationIdsByMetadata = new Dictionary<ConfigurationMetadata, int>();
            }
        }

        /// <summary>
        /// Clears configurations from the configuration cache which have not been explicitly loaded.
        /// </summary>
        /// <returns>Set if configurations which have been cleared.</returns>
        public List<int> ClearNonExplicitlyLoadedConfigurations()
        {
            List<int> configurationIdsCleared = new List<int>();

            Dictionary<int, BuildRequestConfiguration> configurationsToKeep = new Dictionary<int, BuildRequestConfiguration>();
            Dictionary<ConfigurationMetadata, int> configurationIdsByMetadataToKeep = new Dictionary<ConfigurationMetadata, int>();

            lock (_lockObject)
            {
                foreach (KeyValuePair<ConfigurationMetadata, int> metadata in _configurationIdsByMetadata)
                {
                    BuildRequestConfiguration configuration;
                    int configId = metadata.Value;

                    if (_configurations.TryGetValue(configId, out configuration))
                    {
                        // We do not want to retain this configuration
                        if (!configuration.ExplicitlyLoaded)
                        {
                            configurationIdsCleared.Add(configId);
                            configuration.ClearCacheFile();
                            continue;
                        }

                        configurationsToKeep.Add(configId, configuration);
                        configurationIdsByMetadataToKeep.Add(metadata.Key, metadata.Value);
                    }
                }

                _configurations = configurationsToKeep;
                _configurationIdsByMetadata = configurationIdsByMetadataToKeep;
            }

            return configurationIdsCleared;
        }

        /// <summary>
        /// Check whether the config cache has more items that the predefined threshold
        /// </summary>
        public bool IsConfigCacheSizeLargerThanThreshold()
        {
            return _configurations.Count > _sweepThreshhold;
        }

        /// <summary>
        /// Writes out as many configurations to disk as we can, under the assumption that inactive configurations
        /// probably aren't going to be accessed again (the exception is re-entrant builds) and we want to make as much
        /// space as possible now for future projects to load.
        /// </summary>
        /// <returns>True if any configurations were cached, false otherwise.</returns>
        public bool WriteConfigurationsToDisk()
        {
            lock (_lockObject)
            {
                bool cachedAtLeastOneProject = false;

                // Cache 10% of configurations to release some memory
                int remainingToRelease = _configurations.Count;
                if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDENABLEAGGRESSIVECACHING")))
                {
                    // Cache only 10% of configurations to release some memory
                    remainingToRelease = Convert.ToInt32(Math.Max(1, Math.Floor(_configurations.Count * 0.1)));
                }

                foreach (BuildRequestConfiguration configuration in _configurations.Values)
                {
                    if (!configuration.IsCached)
                    {
                        configuration.CacheIfPossible();

                        if (configuration.IsCached)
                        {
                            cachedAtLeastOneProject = true;

                            remainingToRelease--;

                            if (remainingToRelease == 0)
                            {
                                break;
                            }
                        }
                    }
                }

                return cachedAtLeastOneProject;
            }
        }

        #endregion

        #region IEnumerable<BuildRequestConfiguration> Members

        /// <summary>
        /// Gets the enumerator over the configurations in the cache.
        /// </summary>
        public IEnumerator<BuildRequestConfiguration> GetEnumerator()
        {
            return _configurations.Values.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Gets the enumerator over the configurations in the cache.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _configurations.Values.GetEnumerator();
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Sets the component host.
        /// </summary>
        /// <param name="host">The build component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            ErrorUtilities.VerifyThrowArgumentNull(host, nameof(host));
        }

        /// <summary>
        /// Shuts down this component
        /// </summary>
        public void ShutdownComponent()
        {
            lock (_lockObject)
            {
                _configurations.Clear();
            }
        }

        #endregion

        public void Translate(ITranslator translator)
        {
            translator.TranslateDictionary(
                ref _configurations,
                (ITranslator aTranslator, ref int configId) => aTranslator.Translate(ref configId),
                (ITranslator aTranslator, ref BuildRequestConfiguration configuration) =>
                {
                    if (translator.Mode == TranslationDirection.WriteToStream)
                    {
                        configuration.TranslateForFutureUse(aTranslator);
                    }
                    else
                    {
                        configuration = new BuildRequestConfiguration();
                        configuration.TranslateForFutureUse(aTranslator);
                    }
                },
                capacity => new Dictionary<int, BuildRequestConfiguration>(capacity));

            translator.TranslateDictionary(
                ref _configurationIdsByMetadata,
                (ITranslator aTranslator, ref ConfigurationMetadata configMetadata) => aTranslator.Translate(ref configMetadata, ConfigurationMetadata.FactoryForDeserialization),
                (ITranslator aTranslator, ref int configId) => aTranslator.Translate(ref configId),
                capacity => new Dictionary<ConfigurationMetadata, int>(capacity));
        }

        /// <summary>
        /// Factory for component creation.
        /// </summary>
        static internal IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            ErrorUtilities.VerifyThrow(componentType == BuildComponentType.ConfigCache, "Cannot create components of type {0}", componentType);
            return new ConfigCache();
        }

        /// <summary>
        /// Override which determines the key for entry into the collection from the specified build request configuration.
        /// </summary>
        /// <param name="config">The build request configuration.</param>
        /// <returns>The configuration id.</returns>
        protected int GetKeyForConfiguration(BuildRequestConfiguration config)
        {
            return config.ConfigurationId;
        }
    }
}
