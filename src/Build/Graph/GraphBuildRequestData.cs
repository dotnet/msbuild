// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

#nullable disable

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
    public sealed class GraphBuildRequestData
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
        public GraphBuildRequestData(ProjectGraph projectGraph, ICollection<string> targetsToBuild, HostServices hostServices)
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
        public GraphBuildRequestData(ProjectGraph projectGraph, ICollection<string> targetsToBuild, HostServices hostServices, BuildRequestDataFlags flags)
            : this(targetsToBuild, hostServices, flags)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectGraph, nameof(projectGraph));

            ProjectGraph = projectGraph;
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on a project graph.
        /// </summary>
        /// <param name="projectGraph">The graph to build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use, if any.  May be null.</param>
        /// <param name="flags">Flags controlling this build request.</param>
        /// <param name="startingNodes">The nodes to start visiting from.</param>
        /// <param name="direction">The direction to compute the requested graph from the starting nodes.</param>
        /// <param name="graphBuildCacheFilePathDelegate">The delegate to compute the cache filepath from a graph node.</param>
        public GraphBuildRequestData(ProjectGraph projectGraph, ICollection<string> targetsToBuild, HostServices hostServices, BuildRequestDataFlags flags, ICollection<ProjectGraphNode> startingNodes, ProjectGraphNodeDirection direction = ProjectGraphNodeDirection.Current, GraphBuildCacheFilePathDelegate graphBuildCacheFilePathDelegate = null)
            : this(targetsToBuild, hostServices, flags)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectGraph, nameof(projectGraph));
            ErrorUtilities.VerifyThrowArgumentNull(startingNodes, nameof(startingNodes));
            StartingGraphNodes = new List<ProjectGraphNode>(startingNodes);
            ProjectGraphNodeDirection = direction;
            ProjectGraph = projectGraph;
            GraphBuildCacheFilePath = graphBuildCacheFilePathDelegate;

            // Verify that all nodes are part of the project graph
            var allNodes = new HashSet<ProjectGraphNode>(projectGraph.ProjectNodes);
            foreach (var startingGraphNode in StartingGraphNodes)
            {
                ErrorUtilities.VerifyThrowArgument(allNodes.Contains(startingGraphNode), $"The node {startingGraphNode.ProjectInstance.FullPath} is not part of the project graph");
            }
        }

        /// <summary>
        /// Constructs a GraphBuildRequestData for build requests based on project files.
        /// </summary>
        /// <param name="projectFullPath">The full path to the project file.</param>
        /// <param name="globalProperties">The global properties which should be used during evaluation of the project.  Cannot be null.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use.  May be null.</param>
        public GraphBuildRequestData(string projectFullPath, IDictionary<string, string> globalProperties, ICollection<string> targetsToBuild, HostServices hostServices)
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
        public GraphBuildRequestData(string projectFullPath, IDictionary<string, string> globalProperties, ICollection<string> targetsToBuild, HostServices hostServices, BuildRequestDataFlags flags)
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
        public GraphBuildRequestData(ProjectGraphEntryPoint projectGraphEntryPoint, ICollection<string> targetsToBuild, HostServices hostServices)
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
        public GraphBuildRequestData(ProjectGraphEntryPoint projectGraphEntryPoint, ICollection<string> targetsToBuild, HostServices hostServices, BuildRequestDataFlags flags)
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
        public GraphBuildRequestData(IEnumerable<ProjectGraphEntryPoint> projectGraphEntryPoints, ICollection<string> targetsToBuild, HostServices hostServices)
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
        public GraphBuildRequestData(IEnumerable<ProjectGraphEntryPoint> projectGraphEntryPoints, ICollection<string> targetsToBuild, HostServices hostServices, BuildRequestDataFlags flags)
            : this(targetsToBuild, hostServices, flags)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectGraphEntryPoints, nameof(projectGraphEntryPoints));

            ProjectGraphEntryPoints = projectGraphEntryPoints;
        }

        public GraphBuildRequestData(IEnumerable<ProjectGraphEntryPoint> projectGraphEntryPoints, ICollection<string> targetsToBuild, HostServices hostServices, BuildRequestDataFlags flags, GraphBuildOptions graphBuildOptions)
            : this(targetsToBuild, hostServices, flags, graphBuildOptions)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectGraphEntryPoints, nameof(projectGraphEntryPoints));

            ProjectGraphEntryPoints = projectGraphEntryPoints;
        }

        /// <summary>
        /// Common constructor.
        /// </summary>
        private GraphBuildRequestData(ICollection<string> targetsToBuild, HostServices hostServices, BuildRequestDataFlags flags, GraphBuildOptions graphBuildOptions = null)
        {
            ErrorUtilities.VerifyThrowArgumentNull(targetsToBuild, nameof(targetsToBuild));
            foreach (string targetName in targetsToBuild)
            {
                ErrorUtilities.VerifyThrowArgumentNull(targetName, "target");
            }

            HostServices = hostServices;
            TargetNames = new List<string>(targetsToBuild);
            Flags = flags;
            GraphBuildOptions = graphBuildOptions ?? new GraphBuildOptions();
        }

        /// <summary>
        /// The requested project graph to build.
        /// May be null.
        /// </summary>
        /// <value>The project graph.</value>
        public ProjectGraph ProjectGraph { get; }

        /// <summary>
        /// The project graph entry points.
        /// May be null.
        /// </summary>
        /// <value>The project graph entry points.</value>
        public IEnumerable<ProjectGraphEntryPoint> ProjectGraphEntryPoints { get; }

        /// <summary>
        /// The name of the targets to build.
        /// </summary>
        /// <value>An array of targets in the project to be built.</value>
        public ICollection<string> TargetNames { get; }

        /// <summary>
        /// Extra flags for this BuildRequest.
        /// </summary>
        public BuildRequestDataFlags Flags { get; }

        /// <summary>
        /// Options for how the graph should be built.
        /// </summary>
        public GraphBuildOptions GraphBuildOptions { get; }

        /// <summary>
        /// Gets the HostServices object for this request.
        /// </summary>
        public HostServices HostServices { get; }

        /// <summary>
        /// Gets the starting nodes used to visit the graph.
        /// May be null.
        /// </summary>
        public ICollection<ProjectGraphNode> StartingGraphNodes { get; }

        /// <summary>
        /// Gets the starting nodes used to visit the graph.
        /// May be null.
        /// </summary>
        public ProjectGraphNodeDirection ProjectGraphNodeDirection { get; }

        /// <summary>
        /// Gets the delegate to compute the cache filepath from a graph node.
        /// May be null.
        /// </summary>
        public GraphBuildCacheFilePathDelegate GraphBuildCacheFilePath { get; }

        /// <summary>
        /// Gets a boolean indicating if this request requires isolate projects (if <see cref="GraphBuildCacheFilePath"/> is not null)
        /// </summary>
        public bool IsolateProjects => GraphBuildCacheFilePath != null;
    }
}
