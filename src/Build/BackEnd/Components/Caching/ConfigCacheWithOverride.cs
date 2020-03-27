// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    // This class composes two caches, an override cache and a current cache.
    // Reads are served from both caches (override first).
    // Writes should only happen in the current cache.
    internal class ConfigCacheWithOverride : IConfigCache
    {
        private readonly IConfigCache _override;
        private readonly bool _isolateProjects;
        public ConfigCache CurrentCache { get; }

        public ConfigCacheWithOverride(IConfigCache @override, bool isolateProjects)
        {
            _override = @override;
            _isolateProjects = isolateProjects;
            CurrentCache = new ConfigCache();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
            CurrentCache.InitializeComponent(host);
        }

        public void ShutdownComponent()
        {
            CurrentCache.ShutdownComponent();
        }

        public IEnumerator<BuildRequestConfiguration> GetEnumerator()
        {
            // Enumerators do not compose both caches to limit the influence of the override cache (reduce the number of possible states out there).
            // So far all runtime examples do not need the two composed.
            return CurrentCache.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Translate(ITranslator translator)
        {
            ErrorUtilities.ThrowInternalErrorUnreachable();
        }

        public BuildRequestConfiguration this[int configId]
        {
            get
            {
                if (_override.TryGetConfiguration(configId, out var overrideConfig))
                {
                    AssertCurrentCacheDoesNotContainConfig(overrideConfig);

                    return overrideConfig;
                }
                else
                {
                    return CurrentCache[configId];
                }
            }
        }

        public bool TryGetConfiguration(int configId, out BuildRequestConfiguration existingConfig)
        {
            if (_override.TryGetConfiguration(configId, out existingConfig))
            {
                AssertCurrentCacheDoesNotContainConfig(existingConfig);

                return true;
            }

            return CurrentCache.TryGetConfiguration(configId, out existingConfig);
        }

        public void AddConfiguration(BuildRequestConfiguration config)
        {
            CurrentCache.AddConfiguration(config);
        }

        public void RemoveConfiguration(int configId)
        {
            CurrentCache.RemoveConfiguration(configId);
        }

        public BuildRequestConfiguration GetMatchingConfiguration(BuildRequestConfiguration config)
        {
            var overrideConfig = _override.GetMatchingConfiguration(config);

            if (overrideConfig != null)
            {
                AssertCurrentCacheDoesNotContainConfig(overrideConfig);

                return overrideConfig;
            }
            else
            {
                return CurrentCache.GetMatchingConfiguration(config);
            }
        }

        public BuildRequestConfiguration GetMatchingConfiguration(ConfigurationMetadata configMetadata)
        {
            var overrideConfig = _override.GetMatchingConfiguration(configMetadata);

            if (overrideConfig != null)
            {
                AssertCurrentCacheDoesNotContainConfig(overrideConfig);

                return overrideConfig;
            }
            else
            {
                return CurrentCache.GetMatchingConfiguration(configMetadata);
            }
        }

        public BuildRequestConfiguration GetMatchingConfiguration(ConfigurationMetadata configMetadata, ConfigCreateCallback callback, bool loadProject)
        {
            // Call a retrieval method without side effects to avoid creating new entries in the override cache. New entries should go into the current cache.
            var overrideConfig = GetMatchingConfiguration(configMetadata);

            if (overrideConfig != null)
            {
                AssertCurrentCacheDoesNotContainConfig(overrideConfig);

                return overrideConfig;
            }

            return CurrentCache.GetMatchingConfiguration(configMetadata, callback, loadProject);
        }

        public bool HasConfiguration(int configId)
        {
            if (_override.TryGetConfiguration(configId, out var overrideConfig))
            {
                AssertCurrentCacheDoesNotContainConfig(overrideConfig);

                return true;
            }

            return CurrentCache.HasConfiguration(configId);
        }

        public void ClearConfigurations()
        {
            CurrentCache.ClearConfigurations();
        }

        public List<int> ClearNonExplicitlyLoadedConfigurations()
        {
            return CurrentCache.ClearNonExplicitlyLoadedConfigurations();
        }

        public bool IsConfigCacheSizeLargerThanThreshold()
        {
            return CurrentCache.IsConfigCacheSizeLargerThanThreshold();
        }

        public bool WriteConfigurationsToDisk()
        {
            return CurrentCache.WriteConfigurationsToDisk();
        }

        private void AssertCurrentCacheDoesNotContainConfig(BuildRequestConfiguration config)
        {
            if (_isolateProjects)
            {
                ErrorUtilities.VerifyThrow(!CurrentCache.HasConfiguration(config.ConfigurationId), "caches should not overlap");
            }
        }
    }
}
