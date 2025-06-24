// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.ProjectCache
{
    public class ProjectCacheDescriptor
    {
        private ProjectCacheDescriptor(
            string? pluginAssemblyPath,
            IReadOnlyDictionary<string, string>? pluginSettings,
            IProjectCachePluginBase? pluginInstance)
        {
            PluginAssemblyPath = pluginAssemblyPath;
            PluginSettings = pluginSettings ?? new Dictionary<string, string>(0);
            PluginInstance = pluginInstance;
        }

        /// <summary>
        /// Gets the path to the assembly containing the project cache plugin.
        /// </summary>
        public string? PluginAssemblyPath { get; }

        public IReadOnlyDictionary<string, string> PluginSettings { get; }

        public IProjectCachePluginBase? PluginInstance { get; }

        public static ProjectCacheDescriptor FromAssemblyPath(string pluginAssemblyPath, IReadOnlyDictionary<string, string>? pluginSettings = null)
            => new ProjectCacheDescriptor(pluginAssemblyPath, pluginSettings, pluginInstance: null);

        public static ProjectCacheDescriptor FromInstance(ProjectCachePluginBase pluginInstance, IReadOnlyDictionary<string, string>? pluginSettings = null)
            => new ProjectCacheDescriptor(pluginAssemblyPath: null, pluginSettings, pluginInstance);

        [Obsolete("Microsoft.Build.Experimental.ProjectCachePluginBase was moved to Microsoft.Build.ProjectCache, migrate your plugins and use the new type instead.")]
        public static ProjectCacheDescriptor FromInstance(
#pragma warning disable CS0618 // Type or member is obsolete
            Experimental.ProjectCache.ProjectCachePluginBase experimentalPluginInstance,
#pragma warning restore CS0618 // Type or member is obsolete
            IReadOnlyDictionary<string, string>? pluginSettings = null)
        {
            return new ProjectCacheDescriptor(
                pluginAssemblyPath: null,
                pluginSettings,
                experimentalPluginInstance);
        }
    }
}
