// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Graph;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.ProjectCache
{
    public class ProjectCacheDescriptor
    {
        public ProjectCacheDescriptor(
            string pluginPath,
            IReadOnlyCollection<ProjectGraphEntryPoint>? entryPoints,
            ProjectGraph? projectGraph,
            IReadOnlyDictionary<string, string>? pluginSettings = null)
        {
            ErrorUtilities.VerifyThrowArgument(
                (entryPoints == null) ^ (projectGraph == null),
                "EitherEntryPointsOrTheProjectGraphIsSet");

            PluginPath = pluginPath;
            EntryPoints = entryPoints;
            ProjectGraph = projectGraph;
            PluginSettings = pluginSettings ?? new Dictionary<string, string>();
        }

        /// <summary>
        ///     The path to the assembly containing the project cache plugin.
        /// </summary>
        public string PluginPath { get; }

        /// <summary>
        ///     The entry points with which the plugin will be initialized.
        /// </summary>
        public IReadOnlyCollection<ProjectGraphEntryPoint>? EntryPoints { get; }

        /// <summary>
        ///     The graph with which the plugin will be initialized.
        /// </summary>
        public ProjectGraph? ProjectGraph { get; }

        public IReadOnlyDictionary<string, string> PluginSettings { get; }

        public override string ToString()
        {
            var entryPointStyle = EntryPoints != null
                ? "Non static graph based"
                : "Static graph based";

            var entryPoints = EntryPoints != null
                ? string.Join(
                    "\n",
                    EntryPoints.Select(e => $"{e.ProjectFile} {{{FormatGlobalProperties(e.GlobalProperties)}}}"))
                : string.Join(
                    "\n",
                    ProjectGraph!.EntryPointNodes.Select(
                        n =>
                            $"{n.ProjectInstance.FullPath} {{{FormatGlobalProperties(n.ProjectInstance.GlobalProperties)}}}"));

            return $"{PluginPath}\nEntry-point style: {entryPointStyle}\nEntry-points:\n{entryPoints}";

            static string FormatGlobalProperties(IDictionary<string, string> globalProperties)
            {
                return string.Join(", ", globalProperties.Select(gp => $"{gp.Key}={gp.Value}"));
            }
        }
    }
}
