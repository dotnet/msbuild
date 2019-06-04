// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.Graph
{
    /// <summary>
    /// Represents a graph of evaluated projects.
    /// </summary>
    public sealed class ProjectGraph
    {
        /// <summary>
        /// Constructs a graph starting from the given project file, evaluating with the global project collection and no global properties.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/></exception>
        public ProjectGraph(string entryProjectFile)
            : this(new ProjectGraphEntryPoint(entryProjectFile).AsEnumerable(), ProjectCollection.GlobalProjectCollection, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given project files, evaluating with the global project collection and no global properties.
        /// </summary>
        /// <param name="entryProjectFiles">The project files to use as the entry points in constructing the graph</param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/></exception>
        public ProjectGraph(IEnumerable<string> entryProjectFiles)
            : this(ProjectGraphEntryPoint.CreateEnumerable(entryProjectFiles), ProjectCollection.GlobalProjectCollection, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given project file, evaluating with the provided project collection and no global properties.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <param name="projectCollection">The collection with which all projects in the graph should be associated. May not be null.</param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/></exception>
        public ProjectGraph(string entryProjectFile, ProjectCollection projectCollection)
            : this(new ProjectGraphEntryPoint(entryProjectFile).AsEnumerable(), projectCollection, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given project files, evaluating with the provided project collection and no global properties.
        /// </summary>
        /// <param name="entryProjectFiles">The project files to use as the entry points in constructing the graph</param>
        /// <param name="projectCollection">The collection with which all projects in the graph should be associated. May not be null.</param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/></exception>
        public ProjectGraph(IEnumerable<string> entryProjectFiles, ProjectCollection projectCollection)
            : this(ProjectGraphEntryPoint.CreateEnumerable(entryProjectFiles), projectCollection, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given project file, evaluating with the global project collection and no global properties.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <param name="projectCollection">The collection with which all projects in the graph should be associated. May not be null.</param>
        /// <param name="projectInstanceFactory">
        /// A delegate used for constructing a <see cref="ProjectInstance"/>, called for each
        /// project created during graph creation. This value can be null, which uses
        /// a default implementation that calls the ProjectInstance constructor. See the remarks
        /// on the <see cref="ProjectInstanceFactoryFunc"/> for other scenarios.
        /// </param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/>
        /// If a null reference is returned from <paramref name="projectInstanceFactory"/>, the InnerException contains <see cref="InvalidOperationException"/></exception>
        /// </exception>
        public ProjectGraph(string entryProjectFile, ProjectCollection projectCollection, ProjectInstanceFactoryFunc projectInstanceFactory)
            : this(new ProjectGraphEntryPoint(entryProjectFile).AsEnumerable(), projectCollection, projectInstanceFactory)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given project file, evaluating with the provided global properties and the global project collection.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <param name="globalProperties">The global properties to use for all projects. May be null, in which case no global properties will be set.</param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/></exception>
        public ProjectGraph(string entryProjectFile, IDictionary<string, string> globalProperties)
            : this(new ProjectGraphEntryPoint(entryProjectFile, globalProperties).AsEnumerable(), ProjectCollection.GlobalProjectCollection, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given project files, evaluating with the provided global properties and the global project collection.
        /// </summary>
        /// <param name="entryProjectFiles">The project files to use as the entry points in constructing the graph</param>
        /// <param name="globalProperties">The global properties to use for all projects. May be null, in which case no global properties will be set.</param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/></exception>
        public ProjectGraph(IEnumerable<string> entryProjectFiles, IDictionary<string, string> globalProperties)
            : this(ProjectGraphEntryPoint.CreateEnumerable(entryProjectFiles, globalProperties), ProjectCollection.GlobalProjectCollection, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given project file, evaluating with the provided global properties and the provided project collection.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <param name="globalProperties">The global properties to use for all projects. May be null, in which case no global properties will be set.</param>
        /// <param name="projectCollection">The collection with which all projects in the graph should be associated. May not be null.</param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/></exception>
        public ProjectGraph(string entryProjectFile, IDictionary<string, string> globalProperties, ProjectCollection projectCollection)
            : this(new ProjectGraphEntryPoint(entryProjectFile, globalProperties).AsEnumerable(), projectCollection, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given project files, evaluating with the provided global properties and the provided project collection.
        /// </summary>
        /// <param name="entryProjectFiles">The project files to use as the entry points in constructing the graph</param>
        /// <param name="globalProperties">The global properties to use for all projects. May be null, in which case no global properties will be set.</param>
        /// <param name="projectCollection">The collection with which all projects in the graph should be associated. May not be null.</param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/></exception>
        public ProjectGraph(IEnumerable<string> entryProjectFiles, IDictionary<string, string> globalProperties, ProjectCollection projectCollection)
            : this(ProjectGraphEntryPoint.CreateEnumerable(entryProjectFiles, globalProperties), projectCollection, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given graph entry point, evaluating with the global project collection.
        /// </summary>
        /// <param name="entryPoint">The entry point to use in constructing the graph</param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/></exception>
        public ProjectGraph(ProjectGraphEntryPoint entryPoint)
            : this(entryPoint.AsEnumerable(), ProjectCollection.GlobalProjectCollection, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given graph entry points, evaluating with the global project collection.
        /// </summary>
        /// <param name="entryPoints">The entry points to use in constructing the graph</param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/></exception>
        public ProjectGraph(IEnumerable<ProjectGraphEntryPoint> entryPoints)
            : this(entryPoints, ProjectCollection.GlobalProjectCollection, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given graph entry point, evaluating with the provided project collection.
        /// </summary>
        /// <param name="entryPoint">The entry point to use in constructing the graph</param>
        /// <param name="projectCollection">The collection with which all projects in the graph should be associated. May not be null.</param>
        /// <exception cref = "AggregateException" > If the evaluation of any project in the graph fails, the InnerException contains<see cref="InvalidProjectFileException"/></exception>
        public ProjectGraph(ProjectGraphEntryPoint entryPoint, ProjectCollection projectCollection)
            : this(entryPoint.AsEnumerable(), projectCollection, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given graph entry points, evaluating with the provided project collection.
        /// </summary>
        /// <param name="entryPoints">The entry points to use in constructing the graph</param>
        /// <param name="projectCollection">The collection with which all projects in the graph should be associated. May not be null.</param>
        /// <param name="projectInstanceFactory">
        /// A delegate used for constructing a <see cref="ProjectInstance"/>, called for each
        /// project created during graph creation. This value can be null, which uses
        /// a default implementation that calls the ProjectInstance constructor. See the remarks
        /// on <see cref="ProjectInstanceFactoryFunc"/> for other scenarios.
        /// </param>
        /// <exception cref="AggregateException">If the evaluation of any project in the graph fails, the InnerException contains <see cref="InvalidProjectFileException"/>
        /// If a null reference is returned from <paramref name="projectInstanceFactory"/>, the InnerException contains <see cref="InvalidOperationException"/></exception>
        /// <exception cref="CircularDependencyException">If the evaluation is successful but the project graph contains a circular dependency</exception>
        public ProjectGraph(
            IEnumerable<ProjectGraphEntryPoint> entryPoints,
            ProjectCollection projectCollection,
            ProjectInstanceFactoryFunc projectInstanceFactory)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectCollection, nameof(projectCollection));

            projectInstanceFactory = projectInstanceFactory ?? DefaultProjectInstanceFactory;

            _projectInterpretation = ProjectInterpretation.Instance;

            var entryPointConfigurationMetadata = new List<ConfigurationMetadata>();

            foreach (var entryPoint in entryPoints)
            {
                var globalPropertyDictionary = CreatePropertyDictionary(entryPoint.GlobalProperties);

                AddGraphBuildGlobalVariable(globalPropertyDictionary);

                var configurationMetadata = new ConfigurationMetadata(FileUtilities.NormalizePath(entryPoint.ProjectFile), globalPropertyDictionary);
                entryPointConfigurationMetadata.Add(configurationMetadata);
            }

            var (entryPointNodes, rootNodes, allNodes) = LoadGraph(entryPointConfigurationMetadata, projectCollection, projectInstanceFactory, _projectInterpretation);

            EntryPointNodes = entryPointNodes;
            GraphRoots = rootNodes;
            ProjectNodes = allNodes;

            _projectNodesTopologicallySorted = new Lazy<IReadOnlyCollection<ProjectGraphNode>>(() => TopologicalSort(GraphRoots, ProjectNodes));
            
            void AddGraphBuildGlobalVariable(PropertyDictionary<ProjectPropertyInstance> globalPropertyDictionary)
            {
                if (globalPropertyDictionary.GetProperty(PropertyNames.IsGraphBuild) == null)
                {
                    globalPropertyDictionary[PropertyNames.IsGraphBuild] = ProjectPropertyInstance.Create(PropertyNames.IsGraphBuild, "true");
                }
            }
        }

        private
            (IReadOnlyCollection<ProjectGraphNode> EntryPointNodes, IReadOnlyCollection<ProjectGraphNode> RootNodes, IReadOnlyCollection<ProjectGraphNode> AllNodes) LoadGraph(
            List<ConfigurationMetadata> entryPointConfigurationMetadata,
            ProjectCollection projectCollection,
            ProjectInstanceFactoryFunc projectInstanceFactory,
            ProjectInterpretation sdkInfo)
        {
            var allParsedProjects = new ConcurrentDictionary<ConfigurationMetadata, ProjectGraphNode>();
            var tasksInProgress = new ConcurrentDictionary<ConfigurationMetadata, object>();
            var projectsToEvaluate = new ConcurrentQueue<ConfigurationMetadata>();

            foreach (var entryPoint in entryPointConfigurationMetadata)
            {
                projectsToEvaluate.Enqueue(entryPoint);
            }

            if (FindGraphNodes(
                projectsToEvaluate,
                projectCollection,
                tasksInProgress,
                projectInstanceFactory,
                sdkInfo,
                allParsedProjects,
                out var exceptions))
            {
                var entryPointNodes = entryPointConfigurationMetadata.Select(e => allParsedProjects[e]).ToList();

                CreateEdgesAndDetectCycles(entryPointConfigurationMetadata, sdkInfo, allParsedProjects);

                var graphRoots = GetGraphRoots(entryPointNodes);

                ProjectInterpretation.PostProcess(allParsedProjects);

                return (entryPointNodes.AsReadOnly(), graphRoots.AsReadOnly(), allParsedProjects.Values.ToList());
            }
            else
            {
                throw new AggregateException(exceptions);
            }

            List<ProjectGraphNode> GetGraphRoots(List<ProjectGraphNode> entryPointNodes)
            {
                var graphRoots = new List<ProjectGraphNode>(entryPointNodes.Count);
                foreach (var entryPointNode in entryPointNodes)
                {
                    if (entryPointNode.ReferencingProjects.Count == 0)
                    {
                        graphRoots.Add(entryPointNode);
                    }
                }
                return graphRoots;
            }
        }

        /// <remarks>
        ///     Traverse the found nodes and add edges.
        ///     Maintain the state of each node (InProcess and Processed) to detect cycles.
        ///     Returns false if cycles were detected.
        /// </remarks>
        private void CreateEdgesAndDetectCycles(
            List<ConfigurationMetadata> entryPointConfigurationMetadata,
            ProjectInterpretation sdkInfo,
            ConcurrentDictionary<ConfigurationMetadata, ProjectGraphNode> allParsedProjects)
        {
            var nodeStates = new Dictionary<ProjectGraphNode, NodeVisitationState>();

            foreach (var entrypointConfig in entryPointConfigurationMetadata)
            {
                var entryPointNode = allParsedProjects[entrypointConfig];

                if (!nodeStates.ContainsKey(entryPointNode))
                {
                    CreateEdgesAndDetectCyclesForRoot(entryPointNode, entrypointConfig, nodeStates);
                }
                else
                {
                    ErrorUtilities.VerifyThrow(
                        nodeStates[entryPointNode] == NodeVisitationState.Processed,
                        "entrypoints should get processed after a call to detect cycles");
                }
            }

            return;

            (bool success, List<string> projectsInCycle) CreateEdgesAndDetectCyclesForRoot(
                ProjectGraphNode node,
                ConfigurationMetadata nodeConfig,
                IDictionary<ProjectGraphNode, NodeVisitationState> nodeState)
            {
                nodeState[node] = NodeVisitationState.InProcess;

                foreach (var referenceConfig in sdkInfo.GetReferences(node.ProjectInstance))
                {
                    var referenceNode = allParsedProjects[referenceConfig];

                    if (nodeState.TryGetValue(referenceNode, out var projectReferenceNodeState))
                    {
                        // Because this is a depth-first search, we should only encounter new nodes or nodes whose subgraph has been completely processed.
                        // If we encounter a node that is currently being processed(InProcess state), it must be one of the ancestors in a circular dependency.
                        if (projectReferenceNodeState == NodeVisitationState.InProcess)
                        {
                            if (node.Equals(referenceNode))
                            {
                                // the project being evaluated has a reference to itself
                                var selfReferencingProjectString =
                                    FormatCircularDependencyError(new List<string> {node.ProjectInstance.FullPath, node.ProjectInstance.FullPath});
                                throw new CircularDependencyException(
                                    string.Format(
                                        ResourceUtilities.GetResourceString("CircularDependencyInProjectGraph"),
                                        selfReferencingProjectString));
                            }
                            // the project being evaluated has a circular dependency involving multiple projects
                            // add this project to the list of projects involved in cycle 
                            var projectsInCycle = new List<string> {referenceConfig.ProjectFullPath};
                            return (false, projectsInCycle);
                        }
                    }
                    else
                    {
                        // recursively process newly discovered references
                        var loadReference = CreateEdgesAndDetectCyclesForRoot(referenceNode, referenceConfig, nodeState);
                        if (!loadReference.success)
                        {
                            if (loadReference.projectsInCycle[0].Equals(node.ProjectInstance.FullPath))
                            {
                                // we have reached the nth project in the cycle, form error message and throw
                                loadReference.projectsInCycle.Add(referenceConfig.ProjectFullPath);
                                loadReference.projectsInCycle.Add(node.ProjectInstance.FullPath);
                                var errorMessage = FormatCircularDependencyError(loadReference.projectsInCycle);
                                throw new CircularDependencyException(
                                    string.Format(
                                        ResourceUtilities.GetResourceString("CircularDependencyInProjectGraph"),
                                        errorMessage));
                            }
                            // this is one of the projects in the circular dependency
                            // update the list of projects in cycle and return the list to the caller
                            loadReference.projectsInCycle.Add(referenceConfig.ProjectFullPath);
                            return (false, loadReference.projectsInCycle);
                        }
                    }

                    var parsedProjectReference = allParsedProjects[referenceConfig];
                    node.AddProjectReference(parsedProjectReference);
                    parsedProjectReference.AddReferencingProject(node);
                }

                nodeState[node] = NodeVisitationState.Processed;
                return (true, null);
            }
        }

        /// <summary>
        /// Gets the project nodes representing the entry points.
        /// </summary>
        public IReadOnlyCollection<ProjectGraphNode> EntryPointNodes { get; }

        /// <summary>
        /// Get an unordered collection of all project nodes in the graph.
        /// </summary>
        public IReadOnlyCollection<ProjectGraphNode> ProjectNodes { get; }

        internal string ToDot()
        {
            var nodeCount = 0;
            return ToDot(node => nodeCount++.ToString());
        }

        internal string ToDot(Func<ProjectGraphNode, string> nodeIdProvider)
        {
            ErrorUtilities.VerifyThrowArgumentNull(nodeIdProvider, nameof(nodeIdProvider));

            var nodeIds = new ConcurrentDictionary<ProjectGraphNode, string>();

            var sb = new StringBuilder();

            sb.Append("digraph g\n{\n\tnode [shape=box]\n");

            foreach (var node in ProjectNodes)
            {
                var nodeId = nodeIds.GetOrAdd(node, (n, idProvider) => idProvider(n), nodeIdProvider);

                var nodeName = Path.GetFileNameWithoutExtension(node.ProjectInstance.FullPath);
                var globalPropertiesString = string.Join("<br/>", node.ProjectInstance.GlobalProperties.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}={kvp.Value}"));

                sb.AppendLine($"\t{nodeId} [label=<{nodeName}<br/>{globalPropertiesString}>]");

                foreach (var reference in node.ProjectReferences)
                {
                    var referenceId = nodeIds.GetOrAdd(reference, (n, idProvider) => idProvider(n), nodeIdProvider);

                    sb.AppendLine($"\t{nodeId} -> {referenceId}");
                }
            }

            sb.Append("}");

            return sb.ToString();
        }

        private readonly Lazy<IReadOnlyCollection<ProjectGraphNode>> _projectNodesTopologicallySorted;
        private ProjectInterpretation _projectInterpretation;

        private static IReadOnlyCollection<ProjectGraphNode> TopologicalSort(IReadOnlyCollection<ProjectGraphNode> graphRoots, IReadOnlyCollection<ProjectGraphNode> graphNodes)
        {
            var toposort = new List<ProjectGraphNode>(graphNodes.Count);
            var partialRoots = new Queue<ProjectGraphNode>(graphNodes.Count);
            var inDegree = graphNodes.ToDictionary(n => n, n => n.ReferencingProjects.Count);

            foreach (var root in graphRoots)
            {
                partialRoots.Enqueue(root);
            }

            while (partialRoots.Count != 0)
            {
                var partialRoot = partialRoots.Dequeue();

                toposort.Add(partialRoot);

                foreach (var reference in partialRoot.ProjectReferences)
                {
                    if (--inDegree[reference] == 0)
                    {
                        partialRoots.Enqueue(reference);
                    }
                }
            }

            ErrorUtilities.VerifyThrow(toposort.Count == graphNodes.Count, "sorted node count must be equal to total node count");

            toposort.Reverse();

            return toposort;
        }
        
        /// <summary>
        /// Get a topologically sorted collection of all project nodes in the graph.
        /// Referenced projects appear before the referencing projects.
        /// </summary>
        public IReadOnlyCollection<ProjectGraphNode> ProjectNodesTopologicallySorted => _projectNodesTopologicallySorted.Value;

        public IReadOnlyCollection<ProjectGraphNode> GraphRoots { get; }

        /// <summary>
        /// Gets the target list to be executed for every project in the graph, given a particular target list for the entry project.
        /// </summary>
        /// <remarks>
        /// This method uses the ProjectReferenceTargets items to determine the targets to run per node. The results can then be used
        /// to start building each project individually, assuming a given project is built after its references.
        /// </remarks>
        /// <param name="entryProjectTargets">
        /// The target list for the <see cref="GraphRoots"/>. May be null or empty, in which case the entry projects' default targets will be used.
        /// </param>
        /// <returns>A dictionary containing the target list for each node.</returns>
        public IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> GetTargetLists(ICollection<string> entryProjectTargets)
        {
            // Seed the dictionary with empty lists for every node. In this particular case though an empty list means "build nothing" rather than "default targets".
            var targetLists = ProjectNodes.ToDictionary(node => node, node => ImmutableList<string>.Empty);

            var encounteredEdges = new HashSet<ProjectGraphBuildRequest>();
            var edgesToVisit = new Queue<ProjectGraphBuildRequest>();

            // Initial state for the graph roots
            foreach (var entryPointNode in GraphRoots)
            {
                var entryTargets = entryProjectTargets == null || entryProjectTargets.Count == 0
                    ? ImmutableList.CreateRange(entryPointNode.ProjectInstance.DefaultTargets)
                    : ImmutableList.CreateRange(entryProjectTargets);
                var entryEdge = new ProjectGraphBuildRequest(entryPointNode, entryTargets);
                encounteredEdges.Add(entryEdge);
                edgesToVisit.Enqueue(entryEdge);
            }

            // Traverse the entire graph, visiting each edge once.
            while (edgesToVisit.Count > 0)
            {
                var buildRequest = edgesToVisit.Dequeue();
                var node = buildRequest.Node;
                var requestedTargets = buildRequest.RequestedTargets;

                targetLists[node] = targetLists[node].AddRange(requestedTargets);

                // No need to continue if this node has no project references.
                if (node.ProjectReferences.Count == 0)
                {
                    continue;
                }

                // Based on the entry points of this project, determine which targets to propagate down to project references.
                var targetsToPropagate = ProjectInterpretation.TargetsToPropagate.FromProjectAndEntryTargets(node.ProjectInstance, requestedTargets);

                // Queue the project references for visitation, if the edge hasn't already been traversed.
                foreach (var projectReference in node.ProjectReferences)
                {
                    var applicableTargets = targetsToPropagate.GetApplicableTargets(projectReference.ProjectInstance);

                    var projectReferenceEdge = new ProjectGraphBuildRequest(
                        projectReference,
                        ExpandDefaultTargets(applicableTargets, projectReference.ProjectInstance.DefaultTargets));

                    if (encounteredEdges.Add(projectReferenceEdge))
                    {
                        edgesToVisit.Enqueue(projectReferenceEdge);
                    }
                }
            }

            // Dedupe target lists
            var entriesToUpdate = new List<KeyValuePair<ProjectGraphNode, ImmutableList<string>>>();
            foreach (var pair in targetLists)
            {
                var targetList = pair.Value;

                var seenTargets = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                var i = 0;
                while (i < targetList.Count)
                {
                    if (seenTargets.Add(targetList[i]))
                    {
                        i++;
                    }
                    else
                    {
                        targetList = targetList.RemoveAt(i);
                    }
                }

                // Only update if it changed
                if (targetList != pair.Value)
                {
                    entriesToUpdate.Add(new KeyValuePair<ProjectGraphNode, ImmutableList<string>>(pair.Key, targetList));
                }
            }

            // Update in a separate pass to avoid modifying a collection while iterating it.
            foreach (var pair in entriesToUpdate)
            {
                targetLists[pair.Key] = pair.Value;
            }

            return targetLists;
        }

        private static ImmutableList<string> ExpandDefaultTargets(ImmutableList<string> targets, List<string> defaultTargets)
        {
            var i = 0;
            while (i < targets.Count)
            {
                if (targets[i].Equals(MSBuildConstants.DefaultTargetsMarker, StringComparison.OrdinalIgnoreCase))
                {
                    targets = targets
                        .RemoveAt(i)
                        .InsertRange(i, defaultTargets);
                    i += defaultTargets.Count;
                }
                else
                {
                    i++;
                }
            }

            return targets;
        }

        /// <summary>
        /// A callback used for constructing a <see cref="ProjectInstance"/> for a specific
        /// <see cref="ProjectGraphEntryPoint"/> instance.
        /// </summary>
        /// <param name="projectPath">The path to the project file to parse.</param>
        /// <param name="globalProperties">The global properties to be used for creating the ProjectInstance.</param>
        /// <param name="projectCollection">The <see cref="ProjectCollection"/> context for parsing.</param>
        /// <returns>A <see cref="ProjectInstance"/> instance. This value must not be null.</returns>
        /// <remarks>
        /// The default version of this delegate used by ProjectGraph simply calls the
        /// ProjectInstance constructor with information from the parameters. This delegate
        /// is provided as a hook to allow scenarios like creating a <see cref="Project"/>
        /// instance before converting it to a ProjectInstance for use by the ProjectGraph.
        ///
        /// The returned ProjectInstance will be stored and provided with the ProjectGraph.
        /// If this callback chooses to generate an immutable ProjectInstance, e.g. by
        /// using <see cref="Project.CreateProjectInstance()"/> with the flag
        /// <see cref="ProjectInstanceSettings.Immutable"/>, the resulting ProjectGraph
        /// nodes might not be buildable.
        ///
        /// To avoid corruption of the graph and subsequent builds based on the graph:
        /// - all callback parameters must be utilized for creating the ProjectInstance, without any mutations
        /// - the project instance should not be mutated in any way, its state should be a
        /// full fidelity representation of the project file
        /// </remarks>
        public delegate ProjectInstance ProjectInstanceFactoryFunc(
            string projectPath,
            Dictionary<string, string> globalProperties,
            ProjectCollection projectCollection);

        internal static ProjectInstance DefaultProjectInstanceFactory(
            string projectPath,
            Dictionary<string, string> globalProperties,
            ProjectCollection projectCollection)
        {
            return new ProjectInstance(
                projectPath,
                globalProperties,
                MSBuildConstants.CurrentToolsVersion,
                projectCollection);
        }

        private ProjectGraphNode CreateNewNode(
            ConfigurationMetadata configurationMetadata,
            ProjectCollection projectCollection,
            ProjectInstanceFactoryFunc projectInstanceFactory,
            ConcurrentDictionary<ConfigurationMetadata, ProjectGraphNode> allParsedProjects)
        {
            // TODO: ProjectInstance just converts the dictionary back to a PropertyDictionary, so find a way to directly provide it.
            var globalProperties = configurationMetadata.GlobalProperties.ToDictionary();

            var projectInstance = projectInstanceFactory(
                configurationMetadata.ProjectFullPath,
                globalProperties,
                projectCollection);
            if (projectInstance == null)
            {
                throw new InvalidOperationException(ResourceUtilities.GetResourceString("NullReferenceFromProjectInstanceFactory"));
            }

            var graphNode = new ProjectGraphNode(
                projectInstance);
            allParsedProjects[configurationMetadata] = graphNode;
            return graphNode;
        }

        /// <summary>
        /// Load a graph with root node at entryProjectFile
        /// Maintain a queue of projects to be processed and evaluate projects in parallel
        /// Returns false if loading the graph is not successful 
        /// </summary>
        private bool FindGraphNodes(
            ConcurrentQueue<ConfigurationMetadata> projectsToEvaluate,
            ProjectCollection projectCollection,
            ConcurrentDictionary<ConfigurationMetadata, object> tasksInProgress,
            ProjectInstanceFactoryFunc projectInstanceFactory,
            ProjectInterpretation projectInterpretation,
            ConcurrentDictionary<ConfigurationMetadata, ProjectGraphNode> allParsedProjects,
            out List<Exception> exceptions)
        {
            var exceptionsInTasks = new ConcurrentBag<Exception>();
            var evaluationWaitHandle = new AutoResetEvent(false);

            while (projectsToEvaluate.Count != 0 || tasksInProgress.Count != 0)
            {
                ConfigurationMetadata projectToEvaluate;
                if (projectsToEvaluate.Count != 0)
                {
                    projectToEvaluate = projectsToEvaluate.Dequeue();
                    var task = new Task(() =>
                    {
                        var parsedProject = CreateNewNode(projectToEvaluate, projectCollection, projectInstanceFactory, allParsedProjects);

                        foreach (var referenceConfig in projectInterpretation.GetReferences(parsedProject.ProjectInstance))
                        {
                             /*todo: fix the following double check-then-act concurrency bug: one thread can pass the two checks, loose context,
                             meanwhile another thread passes the same checks with the same data and inserts its reference. The initial thread regains context
                             and duplicates the information, leading to wasted work
                             */
                            if (!tasksInProgress.ContainsKey(referenceConfig))
                            {
                                if (!allParsedProjects.ContainsKey(referenceConfig))
                                {
                                    projectsToEvaluate.Enqueue(referenceConfig);
                                    evaluationWaitHandle.Set();
                                }
                            }
                        }
                    });

                    if (tasksInProgress.TryAdd(projectToEvaluate, null))
                    {
                        // once the task completes, remove it from tasksInProgress using a chained task
                        // signal the wait handle to process new projects that have been discovered by this task or exit if all projects have been evaluated
                        task.ContinueWith(_ =>
                        {
                            if (task.IsFaulted)
                            {
                                exceptionsInTasks.Add(task.Exception.InnerException);
                            }
                            tasksInProgress.TryRemove(projectToEvaluate, out var _);
                            evaluationWaitHandle.Set();
                        });
                        task.Start();
                    }
                }
                else
                {
                    // if projectsToEvaluate is empty but there are tasks in progress, there is nothing to do till a task completes and discovers new projects
                    // wait till a task completes and sends a signal
                    evaluationWaitHandle.WaitOne();
                }
            }
            
            if(exceptionsInTasks.Count != 0)
            {
                exceptions = exceptionsInTasks.ToList();
                return false;
            }

            exceptions = null;
            return true;
        }

        private enum NodeVisitationState
        {
            // the project has been evaluated and its project references are being processed
            InProcess,
            // all project references of this project have been processed
            Processed
        }

        internal static string FormatCircularDependencyError(List<string> projectsInCycle)
        {
            const int MAX_PATH = 260;
            var errorMessage = new StringBuilder(projectsInCycle.Count * MAX_PATH);
            errorMessage.AppendLine();
            for (var i = projectsInCycle.Count - 1; i >= 0; i--)
            {
                if (i != 0)
                {
                    errorMessage.Append(projectsInCycle[i]).Append(" ->").AppendLine();
                }
                else
                {
                    errorMessage.Append(projectsInCycle[i]);
                }
            }

            return errorMessage.ToString();
        }

        private static PropertyDictionary<ProjectPropertyInstance> CreatePropertyDictionary(IDictionary<string, string> properties)
        {
            PropertyDictionary<ProjectPropertyInstance> propertyDictionary;
            if (properties == null)
            {
                propertyDictionary = new PropertyDictionary<ProjectPropertyInstance>(0);
            }
            else
            {
                propertyDictionary = new PropertyDictionary<ProjectPropertyInstance>(properties.Count);
                foreach (var entry in properties)
                {
                    propertyDictionary[entry.Key] = ProjectPropertyInstance.Create(entry.Key, entry.Value);
                }
            }

            return propertyDictionary;
        }

        private struct ProjectGraphBuildRequest : IEquatable<ProjectGraphBuildRequest>
        {
            public ProjectGraphBuildRequest(ProjectGraphNode node, ImmutableList<string> targets)
            {
                Node = node ?? throw new ArgumentNullException(nameof(node));
                RequestedTargets = targets ?? throw new ArgumentNullException(nameof(targets));
            }

            public ProjectGraphNode Node { get; }

            public ImmutableList<string> RequestedTargets { get; }

            public bool Equals(ProjectGraphBuildRequest other)
            {
                if (Node != other.Node
                    || RequestedTargets.Count != other.RequestedTargets.Count)
                {
                    return false;
                }

                // Target order is important
                for (var i = 0; i < RequestedTargets.Count; i++)
                {
                    if (!RequestedTargets[i].Equals(other.RequestedTargets[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }

            public override bool Equals(object obj) => !ReferenceEquals(null, obj) && (obj is ProjectGraphBuildRequest graphNodeWithTargets && Equals(graphNodeWithTargets));

            public override int GetHashCode()
            {
                unchecked
                {
                    const int salt = 397;
                    var hashCode = Node.GetHashCode() * salt;
                    for (var i = 0; i < RequestedTargets.Count; i++)
                    {
                        hashCode *= salt;
                        hashCode ^= StringComparer.OrdinalIgnoreCase.GetHashCode(RequestedTargets[i]);
                    }

                    return hashCode;
                }
            }
        }
    }
}
