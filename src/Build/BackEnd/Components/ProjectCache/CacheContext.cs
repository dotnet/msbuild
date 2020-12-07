// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System.Collections.Generic;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.ProjectCache
{
    /// <summary>
    ///     Either Graph is null, or GraphEntryPoints is null. Not Both.
    /// </summary>
    public class CacheContext
    {
        public CacheContext(
            IReadOnlyDictionary<string, string> pluginSettings,
            MSBuildFileSystemBase fileSystem,
            ProjectGraph? graph = null,
            IReadOnlyCollection<ProjectGraphEntryPoint>? graphEntryPoints = null)
        {
            ErrorUtilities.VerifyThrow(
                graph != null ^ graphEntryPoints != null,
                "Either Graph is specified, or GraphEntryPoints is specified. Not both.");

            PluginSettings = pluginSettings;
            Graph = graph;
            GraphEntryPoints = graphEntryPoints;
            MSBuildExePath = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
            FileSystem = fileSystem;
        }

        public IReadOnlyDictionary<string, string> PluginSettings { get; }
        public ProjectGraph? Graph { get; }
        public IReadOnlyCollection<ProjectGraphEntryPoint>? GraphEntryPoints { get; }
        public string MSBuildExePath { get; }
        public MSBuildFileSystemBase FileSystem { get; }
    }
}
