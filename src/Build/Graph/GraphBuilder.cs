// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.Graph
{
    internal class GraphBuilder
    {
        private ProjectInterpretation ProjectInterpretation { get; }

        public IReadOnlyCollection<ProjectGraphNode> ProjectNodes { get; private set; }

        public IReadOnlyCollection<ProjectGraphNode> RootNodes { get; private set; }

        public IReadOnlyCollection<ProjectGraphNode> EntryPointNodes { get; private set; }

        public GraphEdges Edges { get; private set; }

        public GraphBuilder(ProjectInterpretation projectInterpretation)
        {
            ProjectInterpretation = projectInterpretation;
        }

        public void BuildGraph(
            IEnumerable<ProjectGraphEntryPoint> entryPoints,
            ProjectCollection projectCollection,
            ProjectGraph.ProjectInstanceFactoryFunc projectInstanceFactory
            )
        {
            var entryPointConfigurationMetadata = AddGraphBuildPropertyToEntryPoints(entryPoints);

            var allParsedProjects = new ConcurrentDictionary<ConfigurationMetadata, ProjectGraphNode>();
            var projectsToEvaluate = new ConcurrentQueue<ConfigurationMetadata>(entryPointConfigurationMetadata);

            if (FindGraphNodes(
                projectsToEvaluate,
                projectCollection,
                projectInstanceFactory,
                ProjectInterpretation,
                allParsedProjects,
                out var exceptions))
            {
                Edges = new GraphEdges();

                CreateEdgesAndDetectCycles(entryPointConfigurationMetadata, ProjectInterpretation, allParsedProjects, Edges);

                ProjectInterpretation.PostProcess(allParsedProjects, this);

                EntryPointNodes = entryPointConfigurationMetadata.Select(e => allParsedProjects[e]).ToList();
                RootNodes = GetGraphRoots(EntryPointNodes);
                ProjectNodes = allParsedProjects.Values.ToList();
            }
            else
            {
                throw new AggregateException(exceptions);
            }

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
        private void CreateEdgesAndDetectCycles(
            List<ConfigurationMetadata> entryPointConfigurationMetadata,
            ProjectInterpretation sdkInfo,
            ConcurrentDictionary<ConfigurationMetadata, ProjectGraphNode> allParsedProjects,
            GraphEdges edges)
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

                foreach (var (referenceConfig, projectReferenceItem) in sdkInfo.GetReferences(node.ProjectInstance))
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

                    node.AddProjectReference(referenceNode, projectReferenceItem, edges);
                }

                nodeState[node] = NodeVisitationState.Processed;
                return (true, null);
            }
        }

        private ProjectGraphNode CreateNewNode(
            ConfigurationMetadata configurationMetadata,
            ProjectCollection projectCollection,
            ProjectGraph.ProjectInstanceFactoryFunc projectInstanceFactory,
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
        ///     Load a graph with root node at entryProjectFile
        ///     Maintain a queue of projects to be processed and evaluate projects in parallel
        ///     Returns false if loading the graph is not successful
        /// </summary>
        private bool FindGraphNodes(
            ConcurrentQueue<ConfigurationMetadata> projectsToEvaluate,
            ProjectCollection projectCollection,
            ProjectGraph.ProjectInstanceFactoryFunc projectInstanceFactory,
            ProjectInterpretation projectInterpretation,
            ConcurrentDictionary<ConfigurationMetadata, ProjectGraphNode> allParsedProjects,
            out List<Exception> exceptions)
        {
            var tasksInProgress = new ConcurrentDictionary<ConfigurationMetadata, object>();
            var evaluationWaitHandle = new AutoResetEvent(false);
            var exceptionsInTasks = new ConcurrentBag<Exception>();

            while (projectsToEvaluate.Count != 0 || tasksInProgress.Count != 0)
            {
                ConfigurationMetadata projectToEvaluate;
                if (projectsToEvaluate.Count != 0)
                {
                    projectToEvaluate = projectsToEvaluate.Dequeue();
                    var task = new Task(
                        () =>
                        {
                            var parsedProject = CreateNewNode(projectToEvaluate, projectCollection, projectInstanceFactory, allParsedProjects);

                            foreach (var (referenceConfig, _) in projectInterpretation.GetReferences(parsedProject.ProjectInstance))
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
                        task.ContinueWith(
                            _ =>
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

            if (exceptionsInTasks.Count != 0)
            {
                exceptions = exceptionsInTasks.ToList();
                return false;
            }

            exceptions = null;
            return true;
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
