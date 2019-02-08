// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    internal class ConfigCacheWithOverride : IConfigCache
    {
        private readonly IConfigCache _override;
        public ConfigCache CurrentCache { get; }

        public ConfigCacheWithOverride(IConfigCache @override)
        {
            _override = @override;
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
                if (_override.HasConfiguration(configId))
                {
#if DEBUG
                    ErrorUtilities.VerifyThrow(!CurrentCache.HasConfiguration(configId), "caches should not overlap");
#endif
                    return _override[configId];
                }
                else
                {
                    return CurrentCache[configId];
                }
            }
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
#if DEBUG
                ErrorUtilities.VerifyThrow(CurrentCache.GetMatchingConfiguration(config) == null, "caches should not overlap");
#endif
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
#if DEBUG
                ErrorUtilities.VerifyThrow(CurrentCache.GetMatchingConfiguration(configMetadata) == null, "caches should not overlap");
#endif
                return overrideConfig;
            }
            else
            {
                return CurrentCache.GetMatchingConfiguration(configMetadata);
            }
        }

        public BuildRequestConfiguration GetMatchingConfiguration(ConfigurationMetadata configMetadata, ConfigCreateCallback callback, bool loadProject)
        {
            return _override.GetMatchingConfiguration(configMetadata, callback, loadProject) ?? CurrentCache.GetMatchingConfiguration(configMetadata, callback, loadProject);
        }

        public bool HasConfiguration(int configId)
        {
            var overrideHasConfiguration = _override.HasConfiguration(configId);

            if (overrideHasConfiguration)
            {
#if DEBUG
                ErrorUtilities.VerifyThrow(!CurrentCache.HasConfiguration(configId), "caches should not overlap");
#endif
                return overrideHasConfiguration;
            }

            return _override.HasConfiguration(configId) || CurrentCache.HasConfiguration(configId);
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
    }
}
