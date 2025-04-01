// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Graph
{
    internal class GraphBuilder
    {
        internal const string SolutionItemReference = "_SolutionReference";

        /// <summary>
        /// The thread calling BuildGraph() will act as an implicit worker
        /// </summary>
        private const int ImplicitWorkerCount = 1;

        public IReadOnlyCollection<ProjectGraphNode> ProjectNodes { get; private set; }

        public IReadOnlyCollection<ProjectGraphNode> RootNodes { get; private set; }

        public IReadOnlyCollection<ProjectGraphNode> EntryPointNodes { get; private set; }

        public GraphEdges Edges { get; private set; }

        public SolutionFile Solution { get; private set; }

        private readonly List<ConfigurationMetadata> _entryPointConfigurationMetadata;

        private readonly ParallelWorkSet<ConfigurationMetadata, ParsedProject> _graphWorkSet;

        private readonly ProjectCollection _projectCollection;

        private readonly ProjectInterpretation _projectInterpretation;

        private readonly ProjectGraph.ProjectInstanceFactoryFunc _projectInstanceFactory;
        private IReadOnlyDictionary<string, IReadOnlyCollection<string>> _solutionDependencies;
        private ConcurrentDictionary<ConfigurationMetadata, Lazy<ProjectInstance>> _platformNegotiationInstancesCache = new();

        public GraphBuilder(
            IEnumerable<ProjectGraphEntryPoint> entryPoints,
            ProjectCollection projectCollection,
            ProjectGraph.ProjectInstanceFactoryFunc projectInstanceFactory,
            ProjectInterpretation projectInterpretation,
            int degreeOfParallelism,
            CancellationToken cancellationToken)
        {
            var (actualEntryPoints, solutionDependencies) = ExpandSolutionIfPresent(entryPoints.ToImmutableArray());

            _solutionDependencies = solutionDependencies;

            _entryPointConfigurationMetadata = AddGraphBuildPropertyToEntryPoints(actualEntryPoints);

            IEqualityComparer<ConfigurationMetadata> configComparer = EqualityComparer<ConfigurationMetadata>.Default;

            _graphWorkSet = new ParallelWorkSet<ConfigurationMetadata, ParsedProject>(
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

            var allParsedProjects = FindGraphNodes();

            AddEdges(allParsedProjects);

            EntryPointNodes = _entryPointConfigurationMetadata.Select(e => allParsedProjects[e].GraphNode).ToList();

            DetectCycles(EntryPointNodes, _projectInterpretation, allParsedProjects);

            RootNodes = GetGraphRoots(EntryPointNodes);
            ProjectNodes = allParsedProjects.Values.Select(p => p.GraphNode).ToList();

            // Clean and release some temporary used large memory objects.
            _platformNegotiationInstancesCache.Clear();
        }

        private static IReadOnlyCollection<ProjectGraphNode> GetGraphRoots(IReadOnlyCollection<ProjectGraphNode> entryPointNodes)
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

        private void AddEdges(Dictionary<ConfigurationMetadata, ParsedProject> allParsedProjects)
        {
            Edges = new GraphEdges();

            AddEdgesFromProjectReferenceItems(allParsedProjects, Edges);

            _projectInterpretation.AddInnerBuildEdges(allParsedProjects, this);

            if (_solutionDependencies != null && _solutionDependencies.Count != 0)
            {
                AddEdgesFromSolution(allParsedProjects, _solutionDependencies, Edges);
            }
        }

        private void AddEdgesFromProjectReferenceItems(Dictionary<ConfigurationMetadata, ParsedProject> allParsedProjects, GraphEdges edges)
        {
            Dictionary<ProjectGraphNode, HashSet<ProjectGraphNode>> transitiveReferenceCache = new(allParsedProjects.Count);

            foreach (var parsedProject in allParsedProjects)
            {
                var currentNode = parsedProject.Value.GraphNode;

                var requiresTransitiveProjectReferences = _projectInterpretation.RequiresTransitiveProjectReferences(currentNode);

                foreach (var referenceInfo in parsedProject.Value.ReferenceInfos)
                {
                    // Always add direct references.
                    currentNode.AddProjectReference(
                        allParsedProjects[referenceInfo.ReferenceConfiguration].GraphNode,
                        referenceInfo.ProjectReferenceItem,
                        edges);

                    // Add transitive references only if the project requires it.
                    if (requiresTransitiveProjectReferences)
                    {
                        foreach (var transitiveProjectReference in GetTransitiveProjectReferencesExcludingSelf(allParsedProjects[referenceInfo.ReferenceConfiguration]))
                        {
                            currentNode.AddProjectReference(
                                transitiveProjectReference,
                                new ProjectItemInstance(
                                    project: currentNode.ProjectInstance,
                                    itemType: ProjectInterpretation.TransitiveReferenceItemName,
                                    includeEscaped: referenceInfo.ReferenceConfiguration.ProjectFullPath,
                                    directMetadata: null,
                                    definingFileEscaped: currentNode.ProjectInstance.FullPath),
                                edges);
                        }
                    }
                }
            }

            HashSet<ProjectGraphNode> GetTransitiveProjectReferencesExcludingSelf(ParsedProject parsedProject)
            {
                if (transitiveReferenceCache.TryGetValue(parsedProject.GraphNode, out HashSet<ProjectGraphNode> transitiveReferences))
                {
                    return transitiveReferences;
                }

                transitiveReferences = new();

                // Add the results to the cache early, even though it'll be incomplete until the loop below finishes. This helps handle cycles by not allowing them to recurse infinitely.
                // Note that this makes transitive references incomplete in the case of a cycle, but direct dependencies are always added so a cycle will still be detected and an exception will still be thrown.
                transitiveReferenceCache[parsedProject.GraphNode] = transitiveReferences;

                foreach (ProjectInterpretation.ReferenceInfo referenceInfo in parsedProject.ReferenceInfos)
                {
                    ParsedProject reference = allParsedProjects[referenceInfo.ReferenceConfiguration];
                    transitiveReferences.Add(reference.GraphNode);

                    // Perf note: avoiding UnionWith to avoid boxing the HashSet enumerator.
                    foreach (ProjectGraphNode transitiveReference in GetTransitiveProjectReferencesExcludingSelf(reference))
                    {
                        transitiveReferences.Add(transitiveReference);
                    }
                }

                return transitiveReferences;
            }
        }

        private static void AddEdgesFromSolution(IReadOnlyDictionary<ConfigurationMetadata, ParsedProject> allParsedProjects, IReadOnlyDictionary<string, IReadOnlyCollection<string>> solutionDependencies, GraphEdges edges)
        {
            var projectsByPath = new Dictionary<string, List<ProjectGraphNode>>();

            foreach (var project in allParsedProjects)
            {
                var projectPath = project.Value.GraphNode.ProjectInstance.FullPath;

                if (projectsByPath.TryGetValue(projectPath, out List<ProjectGraphNode> value))
                {
                    value.Add(project.Value.GraphNode);
                }
                else
                {
                    projectsByPath[projectPath] = new List<ProjectGraphNode> { project.Value.GraphNode };
                }
            }

            foreach (var solutionDependency in solutionDependencies)
            {
                var referencingProjectPath = solutionDependency.Key;

                ErrorUtilities.VerifyThrow(projectsByPath.TryGetValue(referencingProjectPath, out var referencingNodes), "nodes should include solution projects");

                var referencedNodes = solutionDependency.Value.SelectMany(
                    referencedProjectPath =>
                    {
                        ErrorUtilities.VerifyThrow(projectsByPath.TryGetValue(referencedProjectPath, out List<ProjectGraphNode> projectToReturn), "nodes should include solution projects");
                        return projectToReturn;
                    }).ToArray();

                foreach (var referencingNode in referencingNodes)
                {
                    foreach (var referencedNode in referencedNodes)
                    {
                        var stubItem = new ProjectItemInstance(
                            referencingNode.ProjectInstance,
                            SolutionItemReference,
                            referencedNode.ProjectInstance.FullPath,
                            referencingNode.ProjectInstance.FullPath);

                        referencingNode.AddProjectReference(referencedNode, stubItem, edges);
                    }
                }
            }
        }

        private (IReadOnlyCollection<ProjectGraphEntryPoint> NewEntryPoints, IReadOnlyDictionary<string, IReadOnlyCollection<string>> SolutionDependencies) ExpandSolutionIfPresent(IReadOnlyCollection<ProjectGraphEntryPoint> entryPoints)
        {
            if (entryPoints.Count == 0 || !entryPoints.Any(e => FileUtilities.IsSolutionFilename(e.ProjectFile)))
            {
                return (entryPoints, null);
            }

            if (entryPoints.Count != 1)
            {
                throw new ArgumentException(
                    ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
                        "StaticGraphAcceptsSingleSolutionEntryPoint",
                        string.Join(";", entryPoints.Select(e => e.ProjectFile))));
            }

            ErrorUtilities.VerifyThrowArgument(entryPoints.Count == 1, "StaticGraphAcceptsSingleSolutionEntryPoint");

            ProjectGraphEntryPoint solutionEntryPoint = entryPoints.Single();
            ImmutableDictionary<string, string>.Builder solutionGlobalPropertiesBuilder = ImmutableDictionary.CreateBuilder(
                keyComparer: StringComparer.OrdinalIgnoreCase,
                valueComparer: StringComparer.Ordinal);

            if (solutionEntryPoint.GlobalProperties != null)
            {
                solutionGlobalPropertiesBuilder.AddRange(solutionEntryPoint.GlobalProperties);
            }

            Solution = SolutionFile.Parse(solutionEntryPoint.ProjectFile);

            if (Solution.SolutionParserWarnings.Count != 0 || Solution.SolutionParserErrorCodes.Count != 0)
            {
                throw new InvalidProjectFileException(
                    ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
                        "StaticGraphSolutionLoaderEncounteredSolutionWarningsAndErrors",
                        solutionEntryPoint.ProjectFile,
                        string.Join(";", Solution.SolutionParserWarnings),
                        string.Join(";", Solution.SolutionParserErrorCodes)));
            }

            // Mimic behavior of SolutionProjectGenerator
            SolutionConfigurationInSolution currentSolutionConfiguration = SelectSolutionConfiguration(Solution, solutionEntryPoint.GlobalProperties);
            solutionGlobalPropertiesBuilder["Configuration"] = currentSolutionConfiguration.ConfigurationName;
            solutionGlobalPropertiesBuilder["Platform"] = currentSolutionConfiguration.PlatformName;

            string solutionConfigurationXml = SolutionProjectGenerator.GetSolutionConfiguration(Solution, currentSolutionConfiguration);
            solutionGlobalPropertiesBuilder["CurrentSolutionConfigurationContents"] = solutionConfigurationXml;
            solutionGlobalPropertiesBuilder["BuildingSolutionFile"] = "true";

            string solutionDirectoryName = Solution.SolutionFileDirectory;
            if (!solutionDirectoryName.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                solutionDirectoryName += Path.DirectorySeparatorChar;
            }

            solutionGlobalPropertiesBuilder["SolutionDir"] = EscapingUtilities.Escape(solutionDirectoryName);
            solutionGlobalPropertiesBuilder["SolutionExt"] = EscapingUtilities.Escape(Path.GetExtension(Solution.FullPath));
            solutionGlobalPropertiesBuilder["SolutionFileName"] = EscapingUtilities.Escape(Path.GetFileName(Solution.FullPath));
            solutionGlobalPropertiesBuilder["SolutionName"] = EscapingUtilities.Escape(Path.GetFileNameWithoutExtension(Solution.FullPath));
            solutionGlobalPropertiesBuilder[SolutionProjectGenerator.SolutionPathPropertyName] = EscapingUtilities.Escape(Path.Combine(Solution.SolutionFileDirectory, Path.GetFileName(Solution.FullPath)));

            // Project configurations are reused heavily, so cache the global properties for each
            Dictionary<string, ImmutableDictionary<string, string>> globalPropertiesForProjectConfiguration = new(StringComparer.OrdinalIgnoreCase);

            IReadOnlyList<ProjectInSolution> projectsInSolution = Solution.ProjectsInOrder;
            List<ProjectGraphEntryPoint> newEntryPoints = new(projectsInSolution.Count);
            Dictionary<string, IReadOnlyCollection<string>> solutionDependencies = new();

            foreach (ProjectInSolution project in projectsInSolution)
            {
                if (!SolutionFile.IsBuildableProject(project))
                {
                    continue;
                }

                ProjectConfigurationInSolution projectConfiguration = SelectProjectConfiguration(currentSolutionConfiguration, project.ProjectConfigurations);

                if (!SolutionProjectGenerator.WouldProjectBuild(Solution, currentSolutionConfiguration.FullName, project, projectConfiguration))
                {
                    continue;
                }

                if (!globalPropertiesForProjectConfiguration.TryGetValue(projectConfiguration.FullName, out ImmutableDictionary<string, string> projectGlobalProperties))
                {
                    solutionGlobalPropertiesBuilder["Configuration"] = projectConfiguration.ConfigurationName;
                    solutionGlobalPropertiesBuilder["Platform"] = projectConfiguration.PlatformName;

                    projectGlobalProperties = solutionGlobalPropertiesBuilder.ToImmutable();
                    globalPropertiesForProjectConfiguration.Add(projectConfiguration.FullName, projectGlobalProperties);
                }

                newEntryPoints.Add(new ProjectGraphEntryPoint(project.AbsolutePath, projectGlobalProperties));

                if (project.Dependencies.Count > 0)
                {
                    // code snippet cloned from SolutionProjectGenerator.GetSolutionConfiguration

                    List<string> solutionDependenciesForProject = new(project.Dependencies.Count);
                    foreach (string dependencyProjectGuid in project.Dependencies)
                    {
                        if (!Solution.ProjectsByGuid.TryGetValue(dependencyProjectGuid, out ProjectInSolution dependencyProject))
                        {
                            ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                                "SubCategoryForSolutionParsingErrors",
                                new BuildEventFileInfo(Solution.FullPath),
                                "SolutionParseProjectDepNotFoundError",
                                project.ProjectGuid,
                                dependencyProjectGuid);
                        }

                        // Add it to the list of dependencies, but only if it should build in this solution configuration
                        // (If a project is not selected for build in the solution configuration, it won't build even if it's depended on by something that IS selected for build)
                        // .. and only if it's known to be MSBuild format, as projects can't use the information otherwise
                        if (dependencyProject.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
                        {
                            solutionDependenciesForProject.Add(dependencyProject.AbsolutePath);
                        }
                    }

                    if (solutionDependenciesForProject.Count > 0)
                    {
                        solutionDependencies.Add(project.AbsolutePath, solutionDependenciesForProject);
                    }
                }
            }

            newEntryPoints.TrimExcess();

            return (newEntryPoints, solutionDependencies);

            SolutionConfigurationInSolution SelectSolutionConfiguration(SolutionFile solutionFile, IDictionary<string, string> globalProperties)
            {
                var solutionConfiguration = globalProperties != null && globalProperties.TryGetValue("Configuration", out string configuration)
                    ? configuration
                    : solutionFile.GetDefaultConfigurationName();

                var solutionPlatform = globalProperties != null && globalProperties.TryGetValue("Platform", out string platform)
                    ? platform
                    : solutionFile.GetDefaultPlatformName();

                return new SolutionConfigurationInSolution(solutionConfiguration, solutionPlatform);
            }

            ProjectConfigurationInSolution SelectProjectConfiguration(
                SolutionConfigurationInSolution solutionConfig,
                IReadOnlyDictionary<string, ProjectConfigurationInSolution> projectConfigs)
            {
                // implements the matching described in https://docs.microsoft.com/en-us/visualstudio/ide/understanding-build-configurations?view=vs-2019#how-visual-studio-assigns-project-configuration

                var solutionConfigFullName = solutionConfig.FullName;

                if (projectConfigs.TryGetValue(solutionConfigFullName, out ProjectConfigurationInSolution projectConfiguration))
                {
                    return projectConfiguration;
                }

                var partiallyMarchedConfig = projectConfigs.FirstOrDefault(pc => pc.Value.ConfigurationName.Equals(solutionConfig.ConfigurationName, StringComparison.OrdinalIgnoreCase)).Value;
                return partiallyMarchedConfig ?? projectConfigs.First().Value;
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

                    var configurationMetadata = new ConfigurationMetadata(entryPoint.ProjectFile, globalPropertyDictionary);
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
        ///     Maintain the state of each node (InProcess and Processed) to detect cycles.
        ///     Assumes edges have been added between nodes.
        ///     Returns false if cycles were detected.
        /// </remarks>
        private void DetectCycles(
            IReadOnlyCollection<ProjectGraphNode> entryPointNodes,
            ProjectInterpretation projectInterpretation,
            Dictionary<ConfigurationMetadata, ParsedProject> allParsedProjects)
        {
            var nodeStates = new Dictionary<ProjectGraphNode, NodeVisitationState>();

            foreach (var entryPointNode in entryPointNodes)
            {
                if (!nodeStates.TryGetValue(entryPointNode, out NodeVisitationState state))
                {
                    VisitNode(entryPointNode, nodeStates);
                }
                else
                {
                    ErrorUtilities.VerifyThrow(
                        state == NodeVisitationState.Processed,
                        "entrypoints should get processed after a call to detect cycles");
                }
            }

            return;

            (bool success, List<string> projectsInCycle) VisitNode(
                ProjectGraphNode node,
                IDictionary<ProjectGraphNode, NodeVisitationState> nodeState)
            {
                nodeState[node] = NodeVisitationState.InProcess;

                foreach (var referenceNode in node.ProjectReferences)
                {
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
                                    FormatCircularDependencyError(new List<string> { node.ProjectInstance.FullPath, node.ProjectInstance.FullPath });
                                throw new CircularDependencyException(
                                    string.Format(
                                        ResourceUtilities.GetResourceString("CircularDependencyInProjectGraph"),
                                        selfReferencingProjectString));
                            }

                            // the project being evaluated has a circular dependency involving multiple projects
                            // add this project to the list of projects involved in cycle
                            var projectsInCycle = new List<string> { referenceNode.ProjectInstance.FullPath };
                            return (false, projectsInCycle);
                        }
                    }
                    else
                    {
                        // recursively process newly discovered references
                        var loadReference = VisitNode(referenceNode, nodeState);
                        if (!loadReference.success)
                        {
                            if (loadReference.projectsInCycle[0].Equals(node.ProjectInstance.FullPath))
                            {
                                // we have reached the nth project in the cycle, form error message and throw
                                loadReference.projectsInCycle.Add(referenceNode.ProjectInstance.FullPath);
                                loadReference.projectsInCycle.Add(node.ProjectInstance.FullPath);

                                var errorMessage = FormatCircularDependencyError(loadReference.projectsInCycle);
                                throw new CircularDependencyException(
                                    string.Format(
                                        ResourceUtilities.GetResourceString("CircularDependencyInProjectGraph"),
                                        errorMessage));
                            }

                            // this is one of the projects in the circular dependency
                            // update the list of projects in cycle and return the list to the caller
                            loadReference.projectsInCycle.Add(referenceNode.ProjectInstance.FullPath);
                            return (false, loadReference.projectsInCycle);
                        }
                    }
                }

                nodeState[node] = NodeVisitationState.Processed;
                return (true, null);
            }
        }

        private ParsedProject ParseProject(ConfigurationMetadata configurationMetadata)
        {
            // TODO: ProjectInstance just converts the dictionary back to a PropertyDictionary, so find a way to directly provide it.
            var globalProperties = configurationMetadata.GlobalProperties.ToDictionary();

            var projectInstance = _projectInstanceFactory(
                configurationMetadata.ProjectFullPath,
                globalProperties,
                _projectCollection);

            if (projectInstance == null)
            {
                throw new InvalidOperationException(ResourceUtilities.GetResourceString("NullReferenceFromProjectInstanceFactory"));
            }

            var graphNode = new ProjectGraphNode(projectInstance);

            var referenceInfos = ParseReferences(graphNode);

            return new ParsedProject(configurationMetadata, graphNode, referenceInfos);
        }

        /// <summary>
        ///     Load a graph with root node at entryProjectFile
        ///     Maintain a queue of projects to be processed and evaluate projects in parallel
        ///     Returns false if loading the graph is not successful
        /// </summary>
        private Dictionary<ConfigurationMetadata, ParsedProject> FindGraphNodes()
        {
            foreach (ConfigurationMetadata projectToEvaluate in _entryPointConfigurationMetadata)
            {
                SubmitProjectForParsing(projectToEvaluate);
                /*todo: fix the following double check-then-act concurrency bug: one thread can pass the two checks, loose context,
             meanwhile another thread passes the same checks with the same data and inserts its reference. The initial thread regains context
             and duplicates the information, leading to wasted work
             */
            }

            _graphWorkSet.WaitForAllWorkAndComplete();

            return _graphWorkSet.CompletedWork;
        }

        private void SubmitProjectForParsing(ConfigurationMetadata projectToEvaluate)
        {
            _graphWorkSet.AddWork(projectToEvaluate, () => ParseProject(projectToEvaluate));
        }

        private List<ProjectInterpretation.ReferenceInfo> ParseReferences(ProjectGraphNode parsedProject)
        {
            var referenceInfos = new List<ProjectInterpretation.ReferenceInfo>();

            foreach (var referenceInfo in _projectInterpretation.GetReferences(parsedProject, _projectCollection, GetInstanceForPlatformNegotiationWithCaching))
            {
                if (FileUtilities.IsSolutionFilename(referenceInfo.ReferenceConfiguration.ProjectFullPath))
                {
                    throw new InvalidOperationException(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
                        "StaticGraphDoesNotSupportSlnReferences",
                        referenceInfo.ReferenceConfiguration.ProjectFullPath,
                        referenceInfo.ReferenceConfiguration.ProjectFullPath));
                }

                SubmitProjectForParsing(referenceInfo.ReferenceConfiguration);

                referenceInfos.Add(referenceInfo);
            }

            return referenceInfos;
        }

        private ProjectInstance GetInstanceForPlatformNegotiationWithCaching(
            string projectPath,
            Dictionary<string, string> globalProperties,
            ProjectCollection projectCollection)
        {
            return _platformNegotiationInstancesCache.GetOrAdd(
                new ConfigurationMetadata(projectPath, CreatePropertyDictionary(globalProperties)),
                new Lazy<ProjectInstance>(() => _projectInstanceFactory(projectPath, globalProperties, projectCollection))).Value;
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
                        .AppendLine(" ->");
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

        internal sealed class GraphEdges
        {
            private ConcurrentDictionary<(ProjectGraphNode, ProjectGraphNode), ProjectItemInstance> ReferenceItems =
                new ConcurrentDictionary<(ProjectGraphNode, ProjectGraphNode), ProjectItemInstance>();

            internal int Count => ReferenceItems.Count;

            public ProjectItemInstance this[(ProjectGraphNode node, ProjectGraphNode reference) key]
            {
                get
                {
                    ErrorUtilities.VerifyThrow(ReferenceItems.TryGetValue(key, out ProjectItemInstance referenceItem), "All requested keys should exist");
                    return referenceItem;
                }
            }

            public void AddOrUpdateEdge((ProjectGraphNode node, ProjectGraphNode reference) key, ProjectItemInstance edge)
            {
                ReferenceItems.AddOrUpdate(
                    key,
                    addValueFactory: static ((ProjectGraphNode node, ProjectGraphNode reference) key, ProjectItemInstance referenceItem) => referenceItem,
                    updateValueFactory: static ((ProjectGraphNode node, ProjectGraphNode reference) key, ProjectItemInstance existingItem, ProjectItemInstance newItem) =>
                    {
                        string existingTargetsMetadata = existingItem.GetMetadataValue(ItemMetadataNames.ProjectReferenceTargetsMetadataName);
                        string newTargetsMetadata = newItem.GetMetadataValue(ItemMetadataNames.ProjectReferenceTargetsMetadataName);

                        // Bail out if the targets are the same.
                        if (existingTargetsMetadata.Equals(newTargetsMetadata, StringComparison.OrdinalIgnoreCase))
                        {
                            return existingItem;
                        }

                        existingTargetsMetadata = GetEffectiveTargets(key.reference, existingTargetsMetadata);
                        newTargetsMetadata = GetEffectiveTargets(key.reference, newTargetsMetadata);

                        ProjectItemInstance mergedItem = existingItem.DeepClone();
                        mergedItem.SetMetadata(ItemMetadataNames.ProjectReferenceTargetsMetadataName, $"{existingTargetsMetadata};{newTargetsMetadata}");
                        return mergedItem;

                        static string GetEffectiveTargets(ProjectGraphNode reference, string targetsMetadata)
                        {
                            if (string.IsNullOrWhiteSpace(targetsMetadata))
                            {
                                return string.Join(";", reference.ProjectInstance.DefaultTargets);
                            }

                            return targetsMetadata;
                        }
                    },
                    edge);
            }

            public void RemoveEdge((ProjectGraphNode node, ProjectGraphNode reference) key)
            {
                ErrorUtilities.VerifyThrow(ReferenceItems.TryRemove(key, out _), "All requested keys should exist");
            }

            internal bool HasEdge((ProjectGraphNode node, ProjectGraphNode reference) key) => ReferenceItems.ContainsKey(key);

            internal IReadOnlyDictionary<(ConfigurationMetadata, ConfigurationMetadata), ProjectItemInstance> TestOnly_AsConfigurationMetadata()
            {
                return ReferenceItems.ToImmutableDictionary(
                    kvp => (kvp.Key.Item1.ToConfigurationMetadata(), kvp.Key.Item2.ToConfigurationMetadata()),
                    kvp => kvp.Value);
            }
        }

        private enum NodeVisitationState
        {
            // the project has been evaluated and its project references are being processed
            InProcess,
            // all project references of this project have been processed
            Processed
        }
    }

    internal readonly struct ParsedProject
    {
        public ConfigurationMetadata ConfigurationMetadata { get; }
        public ProjectGraphNode GraphNode { get; }
        public List<ProjectInterpretation.ReferenceInfo> ReferenceInfos { get; }

        public ParsedProject(ConfigurationMetadata configurationMetadata, ProjectGraphNode graphNode, List<ProjectInterpretation.ReferenceInfo> referenceInfos)
        {
            ConfigurationMetadata = configurationMetadata;
            GraphNode = graphNode;
            ReferenceInfos = referenceInfos;
        }
    }
}
