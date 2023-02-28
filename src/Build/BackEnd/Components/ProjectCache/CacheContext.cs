// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.ProjectCache
{
    /// <summary>
    ///     Holds various information about the current msbuild execution that the cache might use.
    ///     The cache may need to know about the top level projects or the entire project graph, so MSBuild
    ///     provides a graph when one is available. When it isn't available, MSBuild provides the top level projects
    ///     and the plugin can construct its own graph based on those.
    ///     So either <see cref="Graph" />is null, or <see cref="GraphEntryPoints" /> is null. But not both.
    /// </summary>
    public class CacheContext
    {
        public IReadOnlyDictionary<string, string> PluginSettings { get; }
        public ProjectGraph? Graph { get; }
        public IReadOnlyCollection<ProjectGraphEntryPoint>? GraphEntryPoints { get; }
        public string? MSBuildExePath { get; }
        public MSBuildFileSystemBase FileSystem { get; }

        public CacheContext(
            IReadOnlyDictionary<string, string> pluginSettings,
            MSBuildFileSystemBase fileSystem,
            ProjectGraph? graph = null,
            IReadOnlyCollection<ProjectGraphEntryPoint>? graphEntryPoints = null)
        {
            ErrorUtilities.VerifyThrow(
                (graph != null) ^ (graphEntryPoints != null),
                "Either Graph is specified, or GraphEntryPoints is specified. Not both.");

            PluginSettings = pluginSettings;
            Graph = graph;
            GraphEntryPoints = graphEntryPoints;
            MSBuildExePath = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
            FileSystem = fileSystem;
        }
    }
}
