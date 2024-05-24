// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Shared;

#nullable disable
#nullable enable

namespace Microsoft.Build.Graph
{
    public record GraphBuildOptions
    {
        /// <summary>
        /// If false, the graph is constructed but the nodes are not built.
        /// </summary>
        public bool Build { get; init; } = true;
    }

    /// <summary>
    /// GraphBuildRequestData encapsulates all of the data needed to submit a graph build request.
    /// </summary>
    public sealed class GraphBuildRequestData : BuildRequestDataBase
    {
        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on a project graph.
        /// </summary>
        /// <param name="projectGraph">The graph to build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        public GraphBuildRequestData(ProjectGraph projectGraph, ICollection<string> targetsToBuild)
            : this(projectGraph, targetsToBuild, null, BuildRequestDataFlags.None)
        {
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on a project graph.
        /// </summary>
        /// <param name="projectGraph">The graph to build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use, if any.  May be null.</param>
        public GraphBuildRequestData(ProjectGraph projectGraph, ICollection<string> targetsToBuild, HostServices? hostServices)
            : this(projectGraph, targetsToBuild, hostServices, BuildRequestDataFlags.None)
        {
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on a project graph.
        /// </summary>
        /// <param name="projectGraph">The graph to build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use, if any.  May be null.</param>
        /// <param name="flags">Flags controlling this build request.</param>
        public GraphBuildRequestData(ProjectGraph projectGraph, ICollection<string> targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags)
            : this(targetsToBuild, hostServices, flags)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectGraph, nameof(projectGraph));

            ProjectGraph = projectGraph;
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on project files.
        /// </summary>
        /// <param name="projectFullPath">The full path to the project file.</param>
        /// <param name="globalProperties">The global properties which should be used during evaluation of the project.  Cannot be null.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use.  May be null.</param>
        public GraphBuildRequestData(string projectFullPath, IDictionary<string, string> globalProperties, ICollection<string> targetsToBuild, HostServices? hostServices)
            : this(new ProjectGraphEntryPoint(projectFullPath, globalProperties).AsEnumerable(), targetsToBuild, hostServices, BuildRequestDataFlags.None)
        {
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on project files.
        /// </summary>
        /// <param name="projectFullPath">The full path to the project file.</param>
        /// <param name="globalProperties">The global properties which should be used during evaluation of the project.  Cannot be null.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use.  May be null.</param>
        /// <param name="flags">The <see cref="BuildRequestDataFlags"/> to use.</param>
        public GraphBuildRequestData(string projectFullPath, IDictionary<string, string> globalProperties, ICollection<string> targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags)
            : this(new ProjectGraphEntryPoint(projectFullPath, globalProperties).AsEnumerable(), targetsToBuild, hostServices, flags)
        {
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on a project graph entry points.
        /// </summary>
        /// <param name="projectGraphEntryPoint">The entry point to use in the build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        public GraphBuildRequestData(ProjectGraphEntryPoint projectGraphEntryPoint, ICollection<string> targetsToBuild)
            : this(projectGraphEntryPoint.AsEnumerable(), targetsToBuild, null, BuildRequestDataFlags.None)
        {
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on a project graph entry points.
        /// </summary>
        /// <param name="projectGraphEntryPoint">The entry point to use in the build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use, if any.  May be null.</param>
        public GraphBuildRequestData(ProjectGraphEntryPoint projectGraphEntryPoint, ICollection<string> targetsToBuild, HostServices? hostServices)
            : this(projectGraphEntryPoint.AsEnumerable(), targetsToBuild, hostServices, BuildRequestDataFlags.None)
        {
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on a project graph entry points.
        /// </summary>
        /// <param name="projectGraphEntryPoint">The entry point to use in the build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use, if any.  May be null.</param>
        /// <param name="flags">Flags controlling this build request.</param>
        public GraphBuildRequestData(ProjectGraphEntryPoint projectGraphEntryPoint, ICollection<string> targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags)
            : this(projectGraphEntryPoint.AsEnumerable(), targetsToBuild, hostServices, flags)
        {
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on a project graph entry points.
        /// </summary>
        /// <param name="projectGraphEntryPoints">The entry points to use in the build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        public GraphBuildRequestData(IEnumerable<ProjectGraphEntryPoint> projectGraphEntryPoints, ICollection<string> targetsToBuild)
            : this(projectGraphEntryPoints, targetsToBuild, null, BuildRequestDataFlags.None)
        {
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on a project graph entry points.
        /// </summary>
        /// <param name="projectGraphEntryPoints">The entry points to use in the build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use, if any.  May be null.</param>
        public GraphBuildRequestData(IEnumerable<ProjectGraphEntryPoint> projectGraphEntryPoints, ICollection<string> targetsToBuild, HostServices? hostServices)
            : this(projectGraphEntryPoints, targetsToBuild, hostServices, BuildRequestDataFlags.None)
        {
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on a project graph entry points.
        /// </summary>
        /// <param name="projectGraphEntryPoints">The entry points to use in the build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use, if any.  May be null.</param>
        /// <param name="flags">Flags controlling this build request.</param>
        public GraphBuildRequestData(IEnumerable<ProjectGraphEntryPoint> projectGraphEntryPoints, ICollection<string> targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags)
            : this(targetsToBuild, hostServices, flags)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectGraphEntryPoints, nameof(projectGraphEntryPoints));

            ProjectGraphEntryPoints = projectGraphEntryPoints;
        }

        public GraphBuildRequestData(IEnumerable<ProjectGraphEntryPoint> projectGraphEntryPoints, ICollection<string> targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags, GraphBuildOptions graphBuildOptions)
            : this(targetsToBuild, hostServices, flags, graphBuildOptions)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectGraphEntryPoints, nameof(projectGraphEntryPoints));

            ProjectGraphEntryPoints = projectGraphEntryPoints.ToList();
        }

        /// <summary>
        /// Common constructor.
        /// </summary>
        private GraphBuildRequestData(ICollection<string> targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags, GraphBuildOptions? graphBuildOptions = null)
        : base(targetsToBuild, flags, hostServices)
        {
            Flags = flags;
            GraphBuildOptions = graphBuildOptions ?? new GraphBuildOptions();
        }

        /// <summary>
        /// The requested project graph to build.
        /// May be null.
        /// </summary>
        /// <value>The project graph.</value>
        public ProjectGraph? ProjectGraph { get; }

        /// <summary>
        /// The project graph entry points.
        /// May be null.
        /// </summary>
        /// <value>The project graph entry points.</value>
        public IEnumerable<ProjectGraphEntryPoint>? ProjectGraphEntryPoints { get; }

        public override IEnumerable<string> EntryProjectsFullPath
        {
            get
            {
                if (ProjectGraph != null)
                {
                    foreach (ProjectGraphNode entryPoint in ProjectGraph.EntryPointNodes)
                    {
                        yield return entryPoint.ProjectInstance.FullPath;
                    }
                }
                else if (ProjectGraphEntryPoints != null)
                {
                    foreach (ProjectGraphEntryPoint entryPoint in ProjectGraphEntryPoints)
                    {
                        yield return entryPoint.ProjectFile;
                    }
                }
            }
        }

        public override IReadOnlyDictionary<string, string?> GlobalPropertiesLookup
        {
            get
            {
                ProjectGraphNode? node = ProjectGraph?.EntryPointNodes.FirstOrDefault();
                if (node != null)
                {
                    return node.ProjectInstance.GlobalProperties.AsReadOnly();
                }

                ProjectGraphEntryPoint? entryPoint = ProjectGraphEntryPoints?.FirstOrDefault();
                if (entryPoint != null)
                {
                    return entryPoint.Value.GlobalProperties.AsReadOnly();
                }

                return ImmutableDictionary<string, string?>.Empty;
            }
        }
        public override bool IsGraphRequest => true;

        /// <summary>
        /// Options for how the graph should be built.
        /// </summary>
        public GraphBuildOptions GraphBuildOptions { get; }
    }
}
