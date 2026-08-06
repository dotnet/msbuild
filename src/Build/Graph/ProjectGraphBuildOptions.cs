// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.Graph
{
    /// <summary>
    /// Options for constructing a target-bound project graph that uses the
    /// generated solution traversal project.
    /// </summary>
    public sealed class ProjectGraphBuildOptions
    {
        /// <summary>
        /// The graph entry points.
        /// </summary>
        public required IEnumerable<ProjectGraphEntryPoint> EntryPoints { get; init; }

        /// <summary>
        /// The project collection used for evaluation and logging.
        /// </summary>
        public required ProjectCollection ProjectCollection { get; init; }

        /// <summary>
        /// The targets that the graph will build.
        /// </summary>
        public required IReadOnlyCollection<string> Targets { get; init; }

        /// <summary>
        /// The graph construction mode.
        /// </summary>
        public ProjectGraphMode Mode { get; init; } = ProjectGraphMode.Default;

        /// <summary>
        /// The degree of parallelism used during graph construction.
        /// </summary>
        public int DegreeOfParallelism { get; init; }
            = NativeMethodsShared.GetLogicalCoreCount();

        /// <summary>
        /// An optional project-instance factory.
        /// </summary>
        public ProjectGraph.ProjectInstanceFactoryFunc? ProjectInstanceFactoryFunc { get; init; }

        /// <summary>
        /// An optional tools-version override for the generated solution project.
        /// </summary>
        public string? ToolsVersionOverride { get; init; }
    }
}