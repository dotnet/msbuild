// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Globalization;
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
        private const string FullPathMetadataName = "FullPath";
        private const string ToolsVersionMetadataName = "ToolsVersion";
        private const string PropertiesMetadataName = "Properties";
        private const string UndefinePropertiesMetadataName = "UndefineProperties";
        private const string AdditionalPropertiesMetadataName = "AdditionalProperties";
        private const string SetConfigurationMetadataName = "SetConfiguration";
        private const string SetPlatformMetadataName = "SetPlatform";
        private const string SetTargetFrameworkMetadataName = "SetTargetFramework";
        private const string GlobalPropertiesToRemoveMetadataName = "GlobalPropertiesToRemove";
        private const string ProjectReferenceTargetsItemType = "ProjectReferenceTargets";
        private const string ProjectReferenceTargetsMetadataName = "Targets";
        private const string DefaultTargetsMarker = ".default";

        private static readonly char[] PropertySeparator = MSBuildConstants.SemicolonChar;

        private readonly ConcurrentDictionary<ConfigurationMetadata, ProjectGraphNode> _allParsedProjects =
            new ConcurrentDictionary<ConfigurationMetadata, ProjectGraphNode>();

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

            var nodeStates = new Dictionary<ProjectGraphNode, NodeState>();
            var entryPointNodes = new List<ProjectGraphNode>();
            var tasksInProgress = new ConcurrentDictionary<ConfigurationMetadata, object>();
            var projectsToEvaluate = new ConcurrentQueue<ConfigurationMetadata>();
            var entryPointConfigurationMetadata = new List<ConfigurationMetadata>();
            foreach (var entryPoint in entryPoints)
            {
                PropertyDictionary<ProjectPropertyInstance> globalPropertyDictionary = CreatePropertyDictionary(entryPoint.GlobalProperties);
                var configurationMetadata = new ConfigurationMetadata(FileUtilities.NormalizePath(entryPoint.ProjectFile), globalPropertyDictionary);
                projectsToEvaluate.Enqueue(configurationMetadata);
                entryPointConfigurationMetadata.Add(configurationMetadata);
            }

            if (LoadGraph(projectsToEvaluate, projectCollection, tasksInProgress, projectInstanceFactory, out List<Exception> exceptions))
            {
                foreach (var configurationMetadata in entryPointConfigurationMetadata)
                {
                    entryPointNodes.Add(_allParsedProjects[configurationMetadata]);
                    if (!nodeStates.TryGetValue(_allParsedProjects[configurationMetadata], out var _))
                    {
                        DetectCycles(_allParsedProjects[configurationMetadata], nodeStates, projectCollection, configurationMetadata.GlobalProperties);
                    }
                }

                var graphRoots = new List<ProjectGraphNode>(entryPointNodes.Count);
                foreach (var entryPointNode in entryPointNodes)
                {
                    if (entryPointNode.ReferencingProjects.Count == 0)
                    {
                        graphRoots.Add(entryPointNode);
                    }
                }

                EntryPointNodes = entryPointNodes.AsReadOnly();
                ProjectNodes = _allParsedProjects.Values.ToList();
                GraphRoots = graphRoots.AsReadOnly();

                _projectNodesTopologicallySorted = new Lazy<IReadOnlyCollection<ProjectGraphNode>>(() => TopologicalSort(GraphRoots, ProjectNodes));
            }
            else
            {
                throw new AggregateException(exceptions);
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

        private readonly Lazy<IReadOnlyCollection<ProjectGraphNode>> _projectNodesTopologicallySorted;

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
        /// <param name="entryProjectTargets">The target list for the entry project. May be null or empty, in which case the entry projects' default targets will be used.</param>
        /// <returns>A dictionary containing the target list for each node.</returns>
        public IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> GetTargetLists(ICollection<string> entryProjectTargets)
        {
            // Seed the dictionary with empty lists for every node. In this particular case though an empty list means "build nothing" rather than "default targets".
            Dictionary<ProjectGraphNode, ImmutableList<string>> targetLists = ProjectNodes.ToDictionary(node => node, node => ImmutableList<string>.Empty);

            var encounteredEdges = new HashSet<ProjectGraphBuildRequest>();
            var edgesToVisit = new Queue<ProjectGraphBuildRequest>();

            // Initial state of the graph traversal.
            foreach (var entryPointNode in EntryPointNodes)
            {
                ImmutableList<string> entryTargets = entryProjectTargets == null || entryProjectTargets.Count == 0
                    ? ImmutableList.CreateRange(entryPointNode.ProjectInstance.DefaultTargets)
                    : ImmutableList.CreateRange(entryProjectTargets);
                var entryEdge = new ProjectGraphBuildRequest(entryPointNode, entryTargets);
                encounteredEdges.Add(entryEdge);
                edgesToVisit.Enqueue(entryEdge);
            }

            // Traverse the entire graph, visiting each edge once.
            while (edgesToVisit.Count > 0)
            {
                ProjectGraphBuildRequest buildRequest = edgesToVisit.Dequeue();
                ProjectGraphNode node = buildRequest.Node;
                ImmutableList<string> requestedTargets = buildRequest.RequestedTargets;

                targetLists[node] = targetLists[node].AddRange(requestedTargets);

                // No need to continue if this node has no project references.
                if (node.ProjectReferences.Count == 0)
                {
                    continue;
                }

                // Based on the entry points of this project, determine which targets to propagate down to project references.
                ImmutableList<string> targetsToPropagate = DetermineTargetsToPropagate(node, requestedTargets);

                // Queue the project references for visitation, if the edge hasn't already been traversed.
                foreach (var projectReference in node.ProjectReferences)
                {
                    var projectReferenceEdge = new ProjectGraphBuildRequest(
                        projectReference,
                        ExpandDefaultTargets(projectReference.ProjectInstance, targetsToPropagate));
                    if (encounteredEdges.Add(projectReferenceEdge))
                    {
                        edgesToVisit.Enqueue(projectReferenceEdge);
                    }
                }
            }

            // Dedupe target lists
            List<KeyValuePair<ProjectGraphNode, ImmutableList<string>>> entriesToUpdate = new List<KeyValuePair<ProjectGraphNode, ImmutableList<string>>>();
            foreach (KeyValuePair<ProjectGraphNode, ImmutableList<string>> pair in targetLists)
            {
                ImmutableList<string> targetList = pair.Value;

                SortedSet<string> seenTargets = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                int i = 0;
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
            foreach (KeyValuePair<ProjectGraphNode, ImmutableList<string>> pair in entriesToUpdate)
            {
                targetLists[pair.Key] = pair.Value;
            }

            return targetLists;
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
            ProjectInstanceFactoryFunc projectInstanceFactory)
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
                projectInstance,
                globalProperties);
            _allParsedProjects[configurationMetadata] = graphNode;
            return graphNode;
        }

        /// <summary>
        /// Load a graph with root node at entryProjectFile
        /// Maintain a queue of projects to be processed and evaluate projects in parallel
        /// Returns false if loading the graph is not successful 
        /// </summary>
        private bool LoadGraph(
            ConcurrentQueue<ConfigurationMetadata> projectsToEvaluate,
            ProjectCollection projectCollection,
            ConcurrentDictionary<ConfigurationMetadata, object> tasksInProgress,
            ProjectInstanceFactoryFunc projectInstanceFactory,
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
                        ProjectGraphNode parsedProject = CreateNewNode(projectToEvaluate, projectCollection, projectInstanceFactory);
                        IEnumerable<ProjectItemInstance> projectReferenceItems = parsedProject.ProjectInstance.GetItems(MSBuildConstants.ProjectReferenceItemName);
                        foreach (var projectReferenceToParse in projectReferenceItems)
                        {
                            if (!string.IsNullOrEmpty(projectReferenceToParse.GetMetadataValue(ToolsVersionMetadataName)))
                            {
                                throw new InvalidOperationException(string.Format(
                                    CultureInfo.InvariantCulture,
                                    ResourceUtilities.GetResourceString(
                                        "ProjectGraphDoesNotSupportProjectReferenceWithToolset"),
                                    projectReferenceToParse.EvaluatedInclude,
                                    parsedProject.ProjectInstance.FullPath));
                            }

                            string projectReferenceFullPath = projectReferenceToParse.GetMetadataValue(FullPathMetadataName);
                            PropertyDictionary<ProjectPropertyInstance> projectReferenceGlobalProperties = GetProjectReferenceGlobalProperties(projectReferenceToParse, projectToEvaluate.GlobalProperties);
                            var projectReferenceConfigurationMetadata = new ConfigurationMetadata(projectReferenceFullPath, projectReferenceGlobalProperties);
                            if (!tasksInProgress.ContainsKey(projectReferenceConfigurationMetadata))
                            {
                                if (!_allParsedProjects.ContainsKey(projectReferenceConfigurationMetadata))
                                {
                                    projectsToEvaluate.Enqueue(projectReferenceConfigurationMetadata);
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

        private enum NodeState
        {
            // the project has been evaluated and its project references are being processed
            InProcess,
            // all project references of this project have been processed
            Processed
        }

        /// <remarks>
        /// Traverse an evaluated graph
        /// Maintain the state of each node (InProcess and Processed) to detect cycles
        /// returns false if loading the graph is not successful
        /// </remarks>
        private (bool success, List<string> projectsInCycle) DetectCycles(ProjectGraphNode node,
            Dictionary<ProjectGraphNode, NodeState> nodeState,
            ProjectCollection projectCollection,
            PropertyDictionary<ProjectPropertyInstance> globalProperties)
        {
            nodeState[node] = NodeState.InProcess;
            IEnumerable<ProjectItemInstance> projectReferenceItems = node.ProjectInstance.GetItems(MSBuildConstants.ProjectReferenceItemName);
            foreach (var projectReferenceToParse in projectReferenceItems)
            {
                string projectReferenceFullPath = projectReferenceToParse.GetMetadataValue(FullPathMetadataName);
                PropertyDictionary<ProjectPropertyInstance> projectReferenceGlobalProperties = GetProjectReferenceGlobalProperties(projectReferenceToParse, globalProperties);
                var projectReferenceConfigurationMetadata = new ConfigurationMetadata(projectReferenceFullPath, projectReferenceGlobalProperties);
                ProjectGraphNode projectReference = _allParsedProjects[projectReferenceConfigurationMetadata];
                if (nodeState.TryGetValue(projectReference, out NodeState projectReferenceNodeState))
                {
                    // Because this is a depth-first search, we should only encounter new nodes or nodes whose subgraph has been completely processed.
                    // If we encounter a node that is currently being processed(InProcess state), it must be one of the ancestors in a circular dependency.
                    if (projectReferenceNodeState == NodeState.InProcess)
                    {
                        if (node.Equals(projectReference))
                        {
                            // the project being evaluated has a reference to itself
                            var selfReferencingProjectString = FormatCircularDependencyError(new List<string> { node.ProjectInstance.FullPath, node.ProjectInstance.FullPath });
                            throw new CircularDependencyException(string.Format(
                                ResourceUtilities.GetResourceString("CircularDependencyInProjectGraph"),
                                selfReferencingProjectString));
                        }
                        else
                        {
                            // the project being evaluated has a circular dependency involving multiple projects
                            // add this project to the list of projects involved in cycle 
                            var projectsInCycle = new List<string> { projectReferenceConfigurationMetadata.ProjectFullPath };
                            return (false, projectsInCycle);
                        }
                    }
                }
                else
                {
                    // recursively process newly discovered references
                    var loadReference = DetectCycles(projectReference, nodeState, projectCollection,
                        projectReferenceGlobalProperties);
                    if (!loadReference.success)
                    {
                        if (loadReference.projectsInCycle[0].Equals(node.ProjectInstance.FullPath))
                        {
                            // we have reached the nth project in the cycle, form error message and throw
                            loadReference.projectsInCycle.Add(projectReferenceConfigurationMetadata.ProjectFullPath);
                            loadReference.projectsInCycle.Add(node.ProjectInstance.FullPath);
                            var errorMessage = FormatCircularDependencyError(loadReference.projectsInCycle);
                            throw new CircularDependencyException(string.Format(
                                ResourceUtilities.GetResourceString("CircularDependencyInProjectGraph"),
                                errorMessage));
                        }
                        else
                        {
                            // this is one of the projects in the circular dependency
                            // update the list of projects in cycle and return the list to the caller
                            loadReference.projectsInCycle.Add(projectReferenceConfigurationMetadata.ProjectFullPath);
                            return (false, loadReference.projectsInCycle);
                        }
                    }
                }
                ProjectGraphNode parsedProjectReference = _allParsedProjects[projectReferenceConfigurationMetadata];
                node.AddProjectReference(parsedProjectReference);
                parsedProjectReference.AddReferencingProject(node);
            }
            nodeState[node] = NodeState.Processed;
            return (true, null);
        }

        internal static string FormatCircularDependencyError(List<string> projectsInCycle)
        {
            const int MAX_PATH = 260;
            var errorMessage = new StringBuilder(projectsInCycle.Count * MAX_PATH);
            errorMessage.AppendLine();
            for (int i = projectsInCycle.Count - 1; i >= 0; i--)
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

        private static ImmutableList<string> DetermineTargetsToPropagate(ProjectGraphNode node, ImmutableList<string> entryTargets)
        {
            var targetsToPropagate = ImmutableList<string>.Empty;
            ICollection<ProjectItemInstance> projectReferenceTargets = node.ProjectInstance.GetItems(ProjectReferenceTargetsItemType);
            foreach (var entryTarget in entryTargets)
            {
                foreach (var projectReferenceTarget in projectReferenceTargets)
                {
                    if (projectReferenceTarget.EvaluatedInclude.Equals(entryTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        string targetsMetadataValue = projectReferenceTarget.GetMetadataValue(ProjectReferenceTargetsMetadataName);
                        targetsToPropagate = targetsToPropagate.AddRange(ExpressionShredder.SplitSemiColonSeparatedList(targetsMetadataValue));
                    }
                }
            }

            return targetsToPropagate;
        }

        private static ImmutableList<string> ExpandDefaultTargets(ProjectInstance project, ImmutableList<string> targets)
        {
            int i = 0;
            while (i < targets.Count)
            {
                if (targets[i].Equals(DefaultTargetsMarker, StringComparison.OrdinalIgnoreCase))
                {
                    targets = targets
                        .RemoveAt(i)
                        .InsertRange(i, project.DefaultTargets);
                    i += project.DefaultTargets.Count;
                }
                else
                {
                    i++;
                }
            }

            return targets;
        }

        /// <summary>
        /// Gets the effective global properties for a project reference.
        /// </summary>
        /// <remarks>
        /// The behavior of this method should match the logic in Microsoft.Common.CurrentVersion.targets and the MSBuild task.
        /// </remarks>
        private static PropertyDictionary<ProjectPropertyInstance> GetProjectReferenceGlobalProperties(ProjectItemInstance projectReference, PropertyDictionary<ProjectPropertyInstance> requesterGlobalProperties)
        {
            string propertiesString = projectReference.GetMetadataValue(PropertiesMetadataName);
            string additionalPropertiesString = projectReference.GetMetadataValue(AdditionalPropertiesMetadataName);
            string undefinePropertiesString = projectReference.GetMetadataValue(UndefinePropertiesMetadataName);
            string globalPropertiesToRemoveString = projectReference.GetMetadataValue(GlobalPropertiesToRemoveMetadataName);

            // The properties on the project reference supersede the ones from the MSBuild task instad of appending.
            if (string.IsNullOrEmpty(propertiesString))
            {
                // TODO: Mimic AssignProjectConfiguration's behavior for determining the values for these.
                string setConfigurationString = projectReference.GetMetadataValue(SetConfigurationMetadataName);
                string setPlatformString = projectReference.GetMetadataValue(SetPlatformMetadataName);
                string setTargetFrameworkString = projectReference.GetMetadataValue(SetTargetFrameworkMetadataName);

                if (!string.IsNullOrEmpty(setConfigurationString) || !string.IsNullOrEmpty(setPlatformString) || !string.IsNullOrEmpty(setTargetFrameworkString))
                {
                    propertiesString = $"{setConfigurationString};{setPlatformString};{setTargetFrameworkString}";
                }
            }

            // If none of these are set, we can just reuse the requestor's global properties directly.
            if (string.IsNullOrEmpty(propertiesString)
                && string.IsNullOrEmpty(additionalPropertiesString)
                && string.IsNullOrEmpty(undefinePropertiesString)
                && string.IsNullOrEmpty(globalPropertiesToRemoveString))
            {
                return requesterGlobalProperties;
            }

            // Make a copy to avoid mutating the requester
            var globalProperties = new PropertyDictionary<ProjectPropertyInstance>(requesterGlobalProperties);

            // Append and remove properties as specified by the various metadata
            MergeIntoPropertyDictionary(globalProperties, propertiesString, PropertiesMetadataName);
            MergeIntoPropertyDictionary(globalProperties, additionalPropertiesString, AdditionalPropertiesMetadataName);
            RemoveFromPropertyDictionary(globalProperties, globalPropertiesToRemoveString);
            RemoveFromPropertyDictionary(globalProperties, undefinePropertiesString);

            return globalProperties;
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
                foreach (KeyValuePair<string, string> entry in properties)
                {
                    propertyDictionary[entry.Key] = ProjectPropertyInstance.Create(entry.Key, entry.Value);
                }
            }

            return propertyDictionary;
        }

        private static void MergeIntoPropertyDictionary(
            PropertyDictionary<ProjectPropertyInstance> properties,
            string propertyNameAndValuesString,
            string syntaxName)
        {
            if (!string.IsNullOrEmpty(propertyNameAndValuesString))
            {
                if (PropertyParser.GetTableWithEscaping(
                    null,
                    null,
                    null,
                    propertyNameAndValuesString.Split(PropertySeparator, StringSplitOptions.RemoveEmptyEntries),
                    out Dictionary<string, string> propertiesTable))
                {
                    foreach (KeyValuePair<string, string> pair in propertiesTable)
                    {
                        properties[pair.Key] = ProjectPropertyInstance.Create(pair.Key, pair.Value);
                    }
                }
                else
                {
                    throw new InvalidProjectFileException(string.Format(
                        CultureInfo.InvariantCulture,
                        ResourceUtilities.GetResourceString("General.InvalidPropertyError"),
                        syntaxName,
                        propertyNameAndValuesString));
                }
            }
        }

        private static void RemoveFromPropertyDictionary(
            PropertyDictionary<ProjectPropertyInstance> properties,
            string propertyNamesString)
        {
            if (!string.IsNullOrEmpty(propertyNamesString))
            {
                var propertiesToRemove = propertyNamesString.Split(PropertySeparator, StringSplitOptions.RemoveEmptyEntries);
                foreach (string propertyName in propertiesToRemove)
                {
                    properties.Remove(propertyName);
                }
            }
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
                for (int i = 0; i < RequestedTargets.Count; i++)
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
                    int hashCode = Node.GetHashCode() * salt;
                    for (int i = 0; i < RequestedTargets.Count; i++)
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
