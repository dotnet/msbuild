// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;

#nullable disable

namespace Microsoft.Build.Execution
{
    internal class ConfigCacheWithOverride : IConfigCache
    {
        private volatile IConfigCache _override;
        public ConfigCache CurrentCache { get; }

        public ConfigCacheWithOverride(IConfigCache @override = null)
        {
            _override = @override;
            CurrentCache = new ConfigCache();
        }

        internal static IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            Assumed.Equal(componentType, BuildComponentType.ConfigCache, $"Cannot create components of type {componentType}");
            return new ConfigCacheWithOverride();
        }

        internal void SetOverride(IConfigCache @override)
        {
            _override = @override;
        }

        internal void ClearOverride()
        {
            _override = null;
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
            CurrentCache.InitializeComponent(host);
        }

        public void ShutdownComponent()
        {
            _override = null;
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
            => Assumed.Unreachable();

        public BuildRequestConfiguration this[int configId]
        {
            get
            {
                IConfigCache overrideCache = _override;
                if (overrideCache is not null && overrideCache.HasConfiguration(configId))
                {
#if DEBUG
                    Assumed.False(CurrentCache.HasConfiguration(configId), "caches should not overlap");
#endif
                    return overrideCache[configId];
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

        public BuildRequestConfiguration GetMatchingConfiguration(BuildRequestConfiguration config)
        {
            IConfigCache overrideCache = _override;
            BuildRequestConfiguration overrideConfig = overrideCache?.GetMatchingConfiguration(config);

            if (overrideConfig is not null)
            {
#if DEBUG
                Assumed.Null(CurrentCache.GetMatchingConfiguration(config), "caches should not overlap");
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
            IConfigCache overrideCache = _override;
            BuildRequestConfiguration overrideConfig = overrideCache?.GetMatchingConfiguration(configMetadata);

            if (overrideConfig is not null)
            {
#if DEBUG
                Assumed.Null(CurrentCache.GetMatchingConfiguration(configMetadata), "caches should not overlap");
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
            IConfigCache overrideCache = _override;
            if (overrideCache?.GetMatchingConfiguration(configMetadata) is not null)
            {
                return overrideCache.GetMatchingConfiguration(configMetadata, callback, loadProject);
            }

            return CurrentCache.GetMatchingConfiguration(configMetadata, callback, loadProject);
        }

        public bool HasConfiguration(int configId)
        {
            IConfigCache overrideCache = _override;
            if (overrideCache is not null && overrideCache.HasConfiguration(configId))
            {
#if DEBUG
                Assumed.False(CurrentCache.HasConfiguration(configId), "caches should not overlap");
#endif
                return true;
            }

            return CurrentCache.HasConfiguration(configId);
        }

        public bool HasConfigurationInOverrideCache(int configId)
        {
            return _override?.HasConfiguration(configId) == true;
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
