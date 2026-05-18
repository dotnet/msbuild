// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.Graph
{
    /// <summary>
    /// Represents the mode to use when constructing a <see cref="ProjectGraph"/>.
    /// </summary>
    public enum ProjectGraphMode
    {
        /// <summary>
        /// Loads only the projects needed for a build, as specified by the entry points. This is the default mode.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Loads a complete representation of the graph, even if they are not needed for the build.
        /// </summary>
        Full = 1,
    }

    /// <summary>
    /// Represents options to use when constructing a <see cref="ProjectGraph" />.
    /// </summary>
    public readonly struct ProjectGraphOptions()
    {
        /// <summary>
        /// The degree of parallelism to use when constructing the graph. Defaults to the number of logical cores on the machine.
        /// </summary>
        public int DegreeOfParallelism { get; init; } = NativeMethodsShared.GetLogicalCoreCount();

        /// <summary>
        /// A list of <see cref="ProjectGraphEntryPoint" /> objects representing the entry points to use when constructing the graph.
        /// </summary>
        public required IEnumerable<ProjectGraphEntryPoint> EntryPoints { get; init; }

        /// <summary>
        /// The <see cref="ProjectGraphMode" /> to use when constructing the graph. Defaults to <see cref="ProjectGraphMode.Default" />.
        /// </summary>
        public ProjectGraphMode Mode { get; init; } = ProjectGraphMode.Default;

        /// <summary>
        /// The <see cref="ProjectCollection" /> to load projects into when constructing the graph. Defaults to <see cref="ProjectCollection.GlobalProjectCollection"/ >.
        /// </summary>
        public ProjectCollection ProjectCollection { get; init; } = ProjectCollection.GlobalProjectCollection;

        /// <summary>
        /// An optional <see cref="ProjectInstanceFactoryFunc" /> to use when evaluating individual projects in the graph.
        /// </summary>
        public ProjectGraph.ProjectInstanceFactoryFunc? ProjectInstanceFactoryFunc { get; init; }
    }
}
