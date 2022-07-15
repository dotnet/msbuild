// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.ProjectCache
{
    internal sealed class ProjectCacheDescriptorEqualityComparer : IEqualityComparer<ProjectCacheDescriptor>
    {
        private ProjectCacheDescriptorEqualityComparer()
        {
        }

        public static ProjectCacheDescriptorEqualityComparer Instance { get; } = new ProjectCacheDescriptorEqualityComparer();

        public bool Equals(ProjectCacheDescriptor? x, ProjectCacheDescriptor? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.PluginAssemblyPath, y.PluginAssemblyPath, StringComparison.OrdinalIgnoreCase)
                && ReferenceEquals(x.PluginInstance, y.PluginInstance)
                && CollectionHelpers.DictionaryEquals(x.PluginSettings, y.PluginSettings);
        }

        public int GetHashCode(ProjectCacheDescriptor obj)
        {
            int hashCode = -1043047289;

            if (obj.PluginAssemblyPath != null)
            {
                hashCode = (hashCode * -1521134295) + StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PluginAssemblyPath);
            }

            if (obj.PluginInstance != null)
            {
                hashCode = (hashCode * -1521134295) + obj.PluginInstance.GetHashCode();
            }

            if (obj.PluginSettings.Count > 0)
            {
                foreach (var pluginSetting in obj.PluginSettings.OrderBy(_ => _.Key))
                {
                    hashCode = (hashCode * -1521134295) + pluginSetting.Key.GetHashCode();
                    hashCode = (hashCode * -1521134295) + pluginSetting.Value.GetHashCode();
                }
            }

            return hashCode;
        }
    }
}
