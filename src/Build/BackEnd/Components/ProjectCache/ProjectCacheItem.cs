// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.ProjectCache
{
    internal class ProjectCacheItem : IEquatable<ProjectCacheItem>
    {
        private readonly IReadOnlyCollection<KeyValuePair<string, string>> _pluginSettingsSorted;

        public ProjectCacheItem(string pluginPath, IReadOnlyDictionary<string, string> pluginSettings)
        {
            PluginPath = pluginPath;

            PluginSettings = pluginSettings;

            // Sort by key to avoid doing it during hashcode computation.
            _pluginSettingsSorted = pluginSettings.OrderBy(_ => _.Key).ToArray();
        }

        public string PluginPath { get; }
        public IReadOnlyDictionary<string, string> PluginSettings { get; }

        public bool Equals(ProjectCacheItem other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return PluginPath == other.PluginPath &&
                   CollectionHelpers.DictionaryEquals(PluginSettings, other.PluginSettings);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ProjectCacheItem) obj);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();

            hashCode.Add(PluginPath);

            foreach (var pluginSetting in _pluginSettingsSorted)
            {
                hashCode.Add(pluginSetting.Key);
                hashCode.Add(pluginSetting.Value);
            }

            return hashCode.ToHashCode();
        }
    }
}
