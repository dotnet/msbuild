// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Experimental.ProjectCache
{
    public class ProjectCacheDescriptor
    {
        private ProjectCacheDescriptor(
            string? pluginAssemblyPath,
            IReadOnlyDictionary<string, string>? pluginSettings,
            ProjectCachePluginBase? pluginInstance)
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

        public ProjectCachePluginBase? PluginInstance { get; }

        public static ProjectCacheDescriptor FromAssemblyPath(string pluginAssemblyPath, IReadOnlyDictionary<string, string>? pluginSettings = null)
            => new ProjectCacheDescriptor(pluginAssemblyPath, pluginSettings, pluginInstance: null);

        public static ProjectCacheDescriptor FromInstance(ProjectCachePluginBase pluginInstance, IReadOnlyDictionary<string, string>? pluginSettings = null)
            => new ProjectCacheDescriptor(pluginAssemblyPath: null, pluginSettings, pluginInstance);
    }
}
