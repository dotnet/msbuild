// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.Graph
{
    internal class GraphBuilder
    {
        /// <summary>
        /// The thread calling BuildGraph() will act as an implicit worker
        /// </summary>
        private const int ImplicitWorkerCount = 1;

        public IReadOnlyCollection<ProjectGraphNode> ProjectNodes { get; private set; }

        public IReadOnlyCollection<ProjectGraphNode> RootNodes { get; private set; }

        public IReadOnlyCollection<ProjectGraphNode> EntryPointNodes { get; private set; }

        public GraphEdges Edges { get; private set; }

        private readonly List<ConfigurationMetadata> _entryPointConfigurationMetadata;

        private readonly ParallelWorkSet<ConfigurationMetadata, ProjectGraphNode> _graphWorkSet;

        private readonly ProjectCollection _projectCollection;

        private readonly ProjectInterpretation _projectInterpretation;

        private readonly ProjectGraph.ProjectInstanceFactoryFunc _projectInstanceFactory;

        public GraphBuilder(
            IEnumerable<ProjectGraphEntryPoint> entryPoints,
            ProjectCollection projectCollection,
            ProjectGraph.ProjectInstanceFactoryFunc projectInstanceFactory,
            ProjectInterpretation projectInterpretation,
            int degreeOfParallelism,
            CancellationToken cancellationToken)
        {
            _entryPointConfigurationMetadata = AddGraphBuildPropertyToEntryPoints(entryPoints);
            IEqualityComparer<ConfigurationMetadata> configComparer = EqualityComparer<ConfigurationMetadata>.Default;

            _graphWorkSet = new ParallelWorkSet<ConfigurationMetadata, ProjectGraphNode>(
                degreeOfParallelism - ImplicitWorkerCount,
                configComparer,
                cancellationToken);
            _projectCollection = projectCollection;
            _projectInstanceFactory = projectInstanceFactory;
            _projectInterpretation = projectInterpretation;
        }

        public void BuildGraph()
        {
            if (_graphWorkSet.IsCompleted)
            {
                return;
            }

            Dictionary<ConfigurationMetadata, ProjectGraphNode> allParsedProjects = FindGraphNodes();
            Edges = CreateEdgesAndDetectCycles(allParsedProjects);
            _projectInterpretation.PostProcess(allParsedProjects, this);

            EntryPointNodes = _entryPointConfigurationMetadata.Select(e => allParsedProjects[e]).ToList();
            RootNodes = GetGraphRoots(EntryPointNodes);
            ProjectNodes = allParsedProjects.Values.ToList();

            IReadOnlyCollection<ProjectGraphNode> GetGraphRoots(IReadOnlyCollection<ProjectGraphNode> entryPointNodes)
            {
                var graphRoots = new List<ProjectGraphNode>(entryPointNodes.Count);

                foreach (var entryPointNode in entryPointNodes)
                {
                    if (entryPointNode.ReferencingProjects.Count == 0)
                    {
                        graphRoots.Add(entryPointNode);
                    }
                }

                graphRoots.TrimExcess();

                return graphRoots;
            }
        }

        private static List<ConfigurationMetadata> AddGraphBuildPropertyToEntryPoints(IEnumerable<ProjectGraphEntryPoint> entryPoints)
        {
            {
                var entryPointConfigurationMetadata = new List<ConfigurationMetadata>();

                foreach (var entryPoint in entryPoints)
                {
                    var globalPropertyDictionary = CreatePropertyDictionary(entryPoint.GlobalProperties);

                    AddGraphBuildGlobalVariable(globalPropertyDictionary);

                    var configurationMetadata = new ConfigurationMetadata(FileUtilities.NormalizePath(entryPoint.ProjectFile), globalPropertyDictionary);
                    entryPointConfigurationMetadata.Add(configurationMetadata);
                }

                return entryPointConfigurationMetadata;
            }

            void AddGraphBuildGlobalVariable(PropertyDictionary<ProjectPropertyInstance> globalPropertyDictionary)
            {
                if (globalPropertyDictionary.GetProperty(PropertyNames.IsGraphBuild) == null)
                {
                    globalPropertyDictionary[PropertyNames.IsGraphBuild] = ProjectPropertyInstance.Create(PropertyNames.IsGraphBuild, "true");
                }
            }
        }

        /// <remarks>
        ///     Traverse the found nodes and add edges.
        ///     Maintain the state of each node (InProcess and Processed) to detect cycles.
        ///     Returns false if cycles were detected.
        /// </remarks>
        private GraphEdges CreateEdgesAndDetectCycles(Dictionary<ConfigurationMetadata, ProjectGraphNode> allParsedProjects)
        {
            var edges = new GraphEdges();
            var nodeStates = new Dictionary<ProjectGraphNode, NodeVisitationState>();

            foreach (ConfigurationMetadata entryPointConfig in _entryPointConfigurationMetadata)
            {
                var entryPointNode = allParsedProjects[entryPointConfig];

                if (!nodeStates.ContainsKey(entryPointNode))
                {
                    CreateEdgesAndDetectCyclesForRoot(entryPointNode, nodeStates);
                }
                else
                {
                    ErrorUtilities.VerifyThrow(
                        nodeStates[entryPointNode] == NodeVisitationState.Processed,
                        "entrypoints should get processed after a call to detect cycles");
                }
            }

            return edges;

            List<string> CreateEdgesAndDetectCyclesForRoot(
                ProjectGraphNode node,
                IDictionary<ProjectGraphNode, NodeVisitationState> nodeState)
            {
                nodeState[node] = NodeVisitationState.InProcess;

                foreach (var (referenceConfig, projectReferenceItem) in _projectInterpretation.GetReferences(node.ProjectInstance))
                {
                    ProjectGraphNode referenceNode = allParsedProjects[referenceConfig];

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
                            return projectsInCycle;
                        }
                    }
                    else
                    {
                        // recursively process newly discovered references
                        List<string> projectsInCycle = CreateEdgesAndDetectCyclesForRoot(referenceNode, nodeState);
                        if (projectsInCycle != null)
                        {
                            if (projectsInCycle[0].Equals(node.ProjectInstance.FullPath))
                            {
                                // we have reached the nth project in the cycle, form error message and throw
                                projectsInCycle.Add(referenceConfig.ProjectFullPath);
                                projectsInCycle.Add(node.ProjectInstance.FullPath);
                                var errorMessage = FormatCircularDependencyError(projectsInCycle);
                                throw new CircularDependencyException(
                                    string.Format(
                                        ResourceUtilities.GetResourceString("CircularDependencyInProjectGraph"),
                                        errorMessage));
                            }
                            // this is one of the projects in the circular dependency
                            // update the list of projects in cycle and return the list to the caller
                            projectsInCycle.Add(referenceConfig.ProjectFullPath);
                            return projectsInCycle;
                        }
                    }

                    node.AddProjectReference(referenceNode, projectReferenceItem, edges);
                }

                nodeState[node] = NodeVisitationState.Processed;
                return null;
            }
        }

        private ProjectGraphNode CreateNewNode(ConfigurationMetadata configurationMetadata)
        {
            // TODO: ProjectInstance just converts the dictionary back to a PropertyDictionary, so find a way to directly provide it.
            var globalProperties = configurationMetadata.GlobalProperties.ToDictionary();

            ProjectInstance projectInstance = _projectInstanceFactory(
                configurationMetadata.ProjectFullPath,
                globalProperties,
                _projectCollection);
            if (projectInstance == null)
            {
                throw new InvalidOperationException(ResourceUtilities.GetResourceString("NullReferenceFromProjectInstanceFactory"));
            }

            var graphNode = new ProjectGraphNode(
                projectInstance);
            ParseReferences(graphNode);

            return graphNode;
        }

        /// <summary>
        ///     Load a graph with root node at entryProjectFile
        ///     Maintain a queue of projects to be processed and evaluate projects in parallel
        ///     Returns false if loading the graph is not successful
        /// </summary>
        private Dictionary<ConfigurationMetadata, ProjectGraphNode> FindGraphNodes()
        {
            foreach (ConfigurationMetadata projectToEvaluate in _entryPointConfigurationMetadata)
            {
                ParseProject(projectToEvaluate);
            }

            _graphWorkSet.WaitForAllWork();
            _graphWorkSet.Complete();
            _graphWorkSet.WaitForCompletion();

            return _graphWorkSet.CompletedWork;
        }

        private void ParseProject(ConfigurationMetadata projectToEvaluate)
        {
            _graphWorkSet.AddWork(projectToEvaluate, () => CreateNewNode(projectToEvaluate));
        }

        private void ParseReferences(ProjectGraphNode parsedProject)
        {
            foreach ((ConfigurationMetadata referenceConfig, _) in _projectInterpretation.GetReferences(parsedProject.ProjectInstance))
            {
                ParseProject(referenceConfig);
            }
        }

        internal static string FormatCircularDependencyError(List<string> projectsInCycle)
        {
            var errorMessage = new StringBuilder(projectsInCycle.Select(p => p.Length).Sum());

            errorMessage.AppendLine();
            for (var i = projectsInCycle.Count - 1; i >= 0; i--)
            {
                if (i != 0)
                {
                    errorMessage.Append(projectsInCycle[i])
                        .Append(" ->")
                        .AppendLine();
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

        internal class GraphEdges
        {
            private ConcurrentDictionary<(ProjectGraphNode, ProjectGraphNode), ProjectItemInstance> ReferenceItems =
                new ConcurrentDictionary<(ProjectGraphNode, ProjectGraphNode), ProjectItemInstance>();

            internal int Count => ReferenceItems.Count;

            public ProjectItemInstance this[(ProjectGraphNode node, ProjectGraphNode reference) key]
            {
                get
                {
                    ErrorUtilities.VerifyThrow(ReferenceItems.ContainsKey(key), "All requested keys should exist");
                    return ReferenceItems[key];
                }

                // First edge wins, in accordance with vanilla msbuild behaviour when multiple msbuild tasks call into the same logical project
                set => ReferenceItems.TryAdd(key, value);
            }

            public void RemoveEdge((ProjectGraphNode node, ProjectGraphNode reference) key)
            {
                ErrorUtilities.VerifyThrow(ReferenceItems.ContainsKey(key), "All requested keys should exist");

                ReferenceItems.TryRemove(key, out _);
            }

            internal bool TestOnly_HasEdge((ProjectGraphNode node, ProjectGraphNode reference) key) => ReferenceItems.ContainsKey(key);
        }

        private enum NodeVisitationState
        {
            // the project has been evaluated and its project references are being processed
            InProcess,
            // all project references of this project have been processed
            Processed
        }
    }
}
