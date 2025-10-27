// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Implements a build request configuration cache.
    /// </summary>
    /// <remarks>
    /// Any methods which performs multiple operations on the cache should:
    /// 1. Take a local reference to the configurations container, in case another thread swaps the instance.
    /// 2. Pair modifications with a TryX method, such that both backing dictionaries are updated in-sync. If this can't
    /// be guaranteed, a new instance should be created to atomically swap the field.
    /// </remarks>
    internal class ConfigCache : IConfigCache
    {
        /// <summary>
        /// Lookup which can be used to find a configuration with the specified ID or metadata.
        /// </summary>
        private Configurations _configurations;

        /// <summary>
        /// The maximum cache entries allowed before a sweep can occur.
        /// </summary>
        private int _sweepThreshhold;

        /// <summary>
        /// Creates a new build configuration cache.
        /// </summary>
        public ConfigCache()
        {
            _configurations = new Configurations();
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
                return _configurations.ById[configId];
            }
        }

        #region IConfigCache Members

        /// <summary>
        /// Adds the specified configuration to the cache.
        /// </summary>
        /// <param name="config">The configuration to add.</param>
        public void AddConfiguration(BuildRequestConfiguration config)
        {
            AddConfiguration(config, _configurations);
        }

        /// <summary>
        /// Helper to add a configuration to the cache with a consistent lookup reference.
        /// </summary>
        private void AddConfiguration(BuildRequestConfiguration config, Configurations configurations)
        {
            ErrorUtilities.VerifyThrowArgumentNull(config);
            ErrorUtilities.VerifyThrow(config.ConfigurationId != 0, "Invalid configuration ID");

            if (!configurations.ById.TryAdd(config.ConfigurationId, config))
            {
                ErrorUtilities.ThrowInternalError("Configuration {0} already cached", config.ConfigurationId);
            }

            _ = configurations.ByMetadata[new ConfigurationMetadata(config)] = config;
        }

        /// <summary>
        /// Returns the entry in the cache which matches the specified config.
        /// </summary>
        /// <param name="config">The configuration to match</param>
        /// <returns>A matching configuration if one exists, null otherwise.</returns>
        public BuildRequestConfiguration GetMatchingConfiguration(BuildRequestConfiguration config)
        {
            ErrorUtilities.VerifyThrowArgumentNull(config);
            return GetMatchingConfiguration(new ConfigurationMetadata(config));
        }

        /// <summary>
        /// Returns the entry in the cache which matches the specified config.
        /// </summary>
        /// <param name="configMetadata">The configuration metadata to match</param>
        /// <returns>A matching configuration if one exists, null otherwise.</returns>
        public BuildRequestConfiguration GetMatchingConfiguration(ConfigurationMetadata configMetadata)
        {
            ErrorUtilities.VerifyThrowArgumentNull(configMetadata);
            if (!_configurations.ByMetadata.TryGetValue(configMetadata, out BuildRequestConfiguration config))
            {
                return null;
            }

            return config;
        }

        /// <summary>
        /// Gets a matching configuration.  If no such configuration exists, one is created and optionally loaded.
        /// </summary>
        public BuildRequestConfiguration GetMatchingConfiguration(ConfigurationMetadata configMetadata, ConfigCreateCallback callback, bool loadProject)
        {
            // Take a local reference to ensure that we are operating on the same lookup instance.
            Configurations configurations = _configurations;

            // If there is no matching configuration, let the caller create one.
            ErrorUtilities.VerifyThrowArgumentNull(configMetadata);
            if (!configurations.ByMetadata.TryGetValue(configMetadata, out BuildRequestConfiguration configuration))
            {
                configuration = callback(null, loadProject);
                AddConfiguration(configuration, configurations);
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

        /// <summary>
        /// Returns true if the cache contains a configuration with the specified id, false otherwise.
        /// </summary>
        /// <param name="configId">The configuration id to check.</param>
        /// <returns>True if the cache contains a configuration with this id, false otherwise.</returns>
        public bool HasConfiguration(int configId)
        {
            return _configurations.ById.ContainsKey(configId);
        }

        /// <summary>
        /// Clear all configurations
        /// </summary>
        public void ClearConfigurations()
        {
            foreach (KeyValuePair<int, BuildRequestConfiguration> config in _configurations.ById)
            {
                config.Value.ClearCacheFile();
            }

            _configurations = new Configurations();
        }

        /// <summary>
        /// Gets the smallest configuration id of any configuration
        /// in this cache.
        /// </summary>
        /// <returns>Gets the smallest configuration id of any
        /// configuration in this cache.</returns>
        public int GetSmallestConfigId()
        {
            Configurations configurations = _configurations;
            ErrorUtilities.VerifyThrow(!configurations.ById.IsEmpty, "No configurations exist from which to obtain the smallest configuration id.");

            int smallestId = int.MaxValue;
            foreach (KeyValuePair<int, BuildRequestConfiguration> kvp in configurations.ById)
            {
                smallestId = Math.Min(smallestId, kvp.Key);
            }

            return smallestId;
        }

        /// <summary>
        /// Clears configurations from the configuration cache which have not been explicitly loaded.
        /// </summary>
        /// <returns>Set if configurations which have been cleared.</returns>
        public List<int> ClearNonExplicitlyLoadedConfigurations()
        {
            List<int> configurationIdsCleared = new List<int>();

            Configurations configurationsToKeep = new();

            foreach (KeyValuePair<ConfigurationMetadata, BuildRequestConfiguration> metadata in _configurations.ByMetadata)
            {
                BuildRequestConfiguration configuration = metadata.Value;
                int configId = configuration.ConfigurationId;

                // We do not want to retain this configuration
                if (!configuration.ExplicitlyLoaded)
                {
                    configurationIdsCleared.Add(configId);
                    configuration.ClearCacheFile();
                    continue;
                }

                configurationsToKeep.ById[configId] = configuration;
                configurationsToKeep.ByMetadata[metadata.Key] = configuration;
            }

            _configurations = configurationsToKeep;
            return configurationIdsCleared;
        }

        /// <summary>
        /// Check whether the config cache has more items that the predefined threshold
        /// </summary>
        public bool IsConfigCacheSizeLargerThanThreshold()
        {
            return _configurations.ById.Count > _sweepThreshhold;
        }

        /// <summary>
        /// Writes out as many configurations to disk as we can, under the assumption that inactive configurations
        /// probably aren't going to be accessed again (the exception is re-entrant builds) and we want to make as much
        /// space as possible now for future projects to load.
        /// </summary>
        /// <returns>True if any configurations were cached, false otherwise.</returns>
        public bool WriteConfigurationsToDisk()
        {
            bool cachedAtLeastOneProject = false;

            // Cache 10% of configurations to release some memory
            Configurations configurations = _configurations;
            int count = configurations.ById.Count;
            int remainingToRelease = count;
            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDENABLEAGGRESSIVECACHING")))
            {
                // Cache only 10% of configurations to release some memory
                remainingToRelease = Convert.ToInt32(Math.Max(1, Math.Floor(count * 0.1)));
            }

            foreach (KeyValuePair<int, BuildRequestConfiguration> kvp in configurations.ById)
            {
                BuildRequestConfiguration configuration = kvp.Value;
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

        #endregion

        #region IEnumerable<BuildRequestConfiguration> Members

        /// <summary>
        /// Gets the enumerator over the configurations in the cache.
        /// </summary>
        public IEnumerator<BuildRequestConfiguration> GetEnumerator()
        {
            // Avoid ConcurrentDictionary.Values here, as it allocates a new snapshot array on each access.
            foreach (KeyValuePair<int, BuildRequestConfiguration> configuration in _configurations.ById)
            {
                yield return configuration.Value;
            }
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Gets the enumerator over the configurations in the cache.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Sets the component host.
        /// </summary>
        /// <param name="host">The build component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            ErrorUtilities.VerifyThrowArgumentNull(host);
        }

        /// <summary>
        /// Shuts down this component
        /// </summary>
        public void ShutdownComponent()
        {
            _configurations = new Configurations();
        }

        #endregion

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _configurations, static _ => new Configurations(_));
        }

        /// <summary>
        /// Factory for component creation.
        /// </summary>
        internal static IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            ErrorUtilities.VerifyThrow(componentType == BuildComponentType.ConfigCache, "Cannot create components of type {0}", componentType);
            return new ConfigCache();
        }

        /// <summary>
        /// A container for thread-safe configuration lookups, such that both fields can be atomically swapped.
        /// </summary>
        private record class Configurations : ITranslatable
        {
            private ConcurrentDictionary<int, BuildRequestConfiguration> _byId;
            private ConcurrentDictionary<ConfigurationMetadata, BuildRequestConfiguration> _byMetadata;

            internal Configurations()
            {
                _byId = new ConcurrentDictionary<int, BuildRequestConfiguration>();
                _byMetadata = new ConcurrentDictionary<ConfigurationMetadata, BuildRequestConfiguration>();
            }

            internal Configurations(ITranslator translator)
            {
                Translate(translator);
            }

            internal ConcurrentDictionary<int, BuildRequestConfiguration> ById => _byId;

            internal ConcurrentDictionary<ConfigurationMetadata, BuildRequestConfiguration> ByMetadata => _byMetadata;

            public void Translate(ITranslator translator)
            {
                // Only serialize one dictionary, as the other can be derived.
                IDictionary<int, BuildRequestConfiguration> configurationsById = _byId;
                translator.TranslateDictionary(
                    ref configurationsById,
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
                    capacity => new ConcurrentDictionary<int, BuildRequestConfiguration>(Environment.ProcessorCount, capacity));

                if (translator.Mode == TranslationDirection.ReadFromStream)
                {
                    _byId = (ConcurrentDictionary<int, BuildRequestConfiguration>)configurationsById;
                    _byMetadata = new ConcurrentDictionary<ConfigurationMetadata, BuildRequestConfiguration>(Environment.ProcessorCount, configurationsById.Count);
                    foreach (KeyValuePair<int, BuildRequestConfiguration> kvp in configurationsById)
                    {
                        _byMetadata[new ConfigurationMetadata(kvp.Value)] = kvp.Value;
                    }
                }
            }
        }
    }
}
