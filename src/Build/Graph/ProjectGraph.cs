// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Graph
{
    /// <summary>
    /// Represents a graph of evaluated projects.
    /// </summary>
    public sealed class ProjectGraph
    {
        private const string ProjectReferenceItemName = "ProjectReference";
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

        private static readonly char[] PropertySeparator = { ';' };

        private readonly Dictionary<ConfigurationMetadata, ProjectGraphNode> _allParsedProjects =
            new Dictionary<ConfigurationMetadata, ProjectGraphNode>();

        /// <summary>
        /// Constructs a graph starting from the given project file, evaluating with the global project collection and no global properties.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <exception cref="InvalidProjectFileException">If the evaluation of any project in the graph fails.</exception>
        public ProjectGraph(string entryProjectFile)
            : this(entryProjectFile, null, ProjectCollection.GlobalProjectCollection)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given project file, evaluating with the provided global properties and the global project collection.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <param name="globalProperties">The global properties to use for all projects. May be null, in which case the containing project collection's global properties will be used.</param>
        /// <exception cref="InvalidProjectFileException">If the evaluation of any project in the graph fails.</exception>
        public ProjectGraph(
            string entryProjectFile,
            IDictionary<string, string> globalProperties)
            : this(entryProjectFile, globalProperties, ProjectCollection.GlobalProjectCollection)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given project file, evaluating with the provided global properties and the provided project collection.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <param name="globalProperties">The global properties to use for all projects. May be null, in which case the containing project collection's global properties will be used.</param>
        /// <param name="projectCollection">The collection with which all projects in the graph should be associated. May not be null.</param>
        /// <exception cref="InvalidProjectFileException">If the evaluation of any project in the graph fails.</exception>
        public ProjectGraph(
            string entryProjectFile,
            IDictionary<string, string> globalProperties,
            ProjectCollection projectCollection)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectCollection, nameof(projectCollection));

            PropertyDictionary<ProjectPropertyInstance> globalPropertyDictionary;
            if (globalProperties == null)
            {
                globalPropertyDictionary = new PropertyDictionary<ProjectPropertyInstance>(0);
            }
            else
            {
                globalPropertyDictionary = new PropertyDictionary<ProjectPropertyInstance>(globalProperties.Count);
                foreach (KeyValuePair<string, string> entry in globalProperties)
                {
                    globalPropertyDictionary[entry.Key] = ProjectPropertyInstance.Create(entry.Key, entry.Value);
                }
            }

            var entryProjectConfigurationMetadata = new ConfigurationMetadata(FileUtilities.NormalizePath(entryProjectFile), globalPropertyDictionary);
            var nodeStates = new Dictionary<ConfigurationMetadata, NodeState>();
            LoadGraph(entryProjectConfigurationMetadata, nodeStates, projectCollection, globalPropertyDictionary);
            EntryProjectNode = _allParsedProjects[entryProjectConfigurationMetadata];
            ProjectNodes = _allParsedProjects.Values;
        }

        /// <summary>
        /// Gets the project node representing the entry point.
        /// </summary>
        public ProjectGraphNode EntryProjectNode { get; }

        /// <summary>
        /// Get an unordered collection of all project nodes in the graph.
        /// </summary>
        public IReadOnlyCollection<ProjectGraphNode> ProjectNodes { get; }

        /// <summary>
        /// Gets the target list to be executed for every project in the graph, given a particular target list for the entry project.
        /// </summary>
        /// <remarks>
        /// This method uses the ProjectReferenceTargets items to determine the targets to run per node. The results can then be used
        /// to start building each project individually, assuming a given project is built after its references.
        /// </remarks>
        /// <param name="entryProjectTargets">The target list for the entry project. May be null or empty, in which case the project's default targets will be used.</param>
        /// <returns>A dictionary containing the target list for each node.</returns>
        public IReadOnlyDictionary<ProjectGraphNode, ImmutableList<string>> GetTargetLists(string[] entryProjectTargets)
        {
            ImmutableList<string> entryTargets = entryProjectTargets == null || entryProjectTargets.Length == 0
                ? ImmutableList.CreateRange(EntryProjectNode.Project.DefaultTargets)
                : ImmutableList.CreateRange(entryProjectTargets);

            // Seed the dictionary with empty lists for every node. In this particular case though an empty list means "build nothing" rather than "default targets".
            Dictionary<ProjectGraphNode, ImmutableList<string>> targetLists = ProjectNodes.ToDictionary(node => node, node => ImmutableList<string>.Empty);

            // Initial state of the graph traversal.
            var entryEdge = new ProjectGraphBuildRequest(EntryProjectNode, entryTargets);
            var encounteredEdges = new HashSet<ProjectGraphBuildRequest>();
            encounteredEdges.Add(entryEdge);
            var edgesToVisit = new Queue<ProjectGraphBuildRequest>();
            edgesToVisit.Enqueue(entryEdge);

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
                        ExpandDefaultTargets(projectReference.Project, targetsToPropagate));
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

        private ProjectGraphNode CreateNewNode(
            ConfigurationMetadata configurationMetadata,
            ProjectCollection projectCollection)
        {
            // TODO: ProjectInstance just converts the dictionary back to a PropertyDictionary, so find a way to directly provide it.
            var globalProperties = configurationMetadata.GlobalProperties.ToDictionary();
            var project = new ProjectInstance(
                configurationMetadata.ProjectFullPath,
                globalProperties,
                configurationMetadata.ToolsVersion,
                projectCollection);
            var graphNode = new ProjectGraphNode(
                project,
                globalProperties);
            _allParsedProjects.Add(configurationMetadata, graphNode);
            return graphNode;
        }

        private enum NodeState
        {
            // the project has been evaluated and it's project references are being processed
            InProcess,
            // all project references of this project have been processed
            Processed
        }

        /// <summary>
        /// Load a graph with root node at entryProjectFile
        /// Maintain the state of each node (InProcess and Processed) to detect cycles
        /// returns false if loading the graph is not successful
        /// </summary>
        private (bool success, List<string> projectsInCycle) LoadGraph(ConfigurationMetadata projectToEvaluate,
            Dictionary<ConfigurationMetadata, NodeState> nodeState,
            ProjectCollection projectCollection,
            PropertyDictionary<ProjectPropertyInstance> globalProperties)
        {
            nodeState[projectToEvaluate] = NodeState.InProcess;
            ProjectGraphNode parsedProject = CreateNewNode(projectToEvaluate, projectCollection);
            IEnumerable<ProjectItemInstance> projectReferenceItems =
                parsedProject.Project.GetItems(ProjectReferenceItemName);
            foreach (var projectReferenceToParse in projectReferenceItems)
            {
                if (!string.IsNullOrEmpty(projectReferenceToParse.GetMetadataValue(ToolsVersionMetadataName)))
                {
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.InvariantCulture,
                        ResourceUtilities.GetResourceString(
                            "ProjectGraphDoesNotSupportProjectReferenceWithToolset"),
                        projectReferenceToParse.EvaluatedInclude,
                        parsedProject.Project.FullPath));
                }

                string projectReferenceFullPath = projectReferenceToParse.GetMetadataValue(FullPathMetadataName);

                PropertyDictionary<ProjectPropertyInstance> projectReferenceGlobalProperties =
                    GetProjectReferenceGlobalProperties(projectReferenceToParse, globalProperties);
                var projectReferenceConfigurationMetadata =
                    new ConfigurationMetadata(projectReferenceFullPath, projectReferenceGlobalProperties);
                if (nodeState.TryGetValue(projectReferenceConfigurationMetadata, out NodeState projectReferenceNodeState))
                {
                    // a project reference can be in "Processed" state. If it is "InProcess" state, it is an ancestor and there is a circular dependency
                    if (projectReferenceNodeState == NodeState.InProcess)
                    {
                        if (projectToEvaluate.ProjectFullPath.Equals(projectReferenceConfigurationMetadata.ProjectFullPath))
                        {
                            // the project being evaluated has a reference on itself
                            throw new CircularDependencyException(string.Format(
                                ResourceUtilities.GetResourceString("CircularDependencyInProjectGraph"),
                                projectToEvaluate.ProjectFullPath));
                        }
                        else
                        {
                            // the project being evaluated has a circular dependency involving multiple projects
                            // add this project to the list of projects involved in cycle 
                            var projectsInCycle = new List<string> {projectReferenceConfigurationMetadata.ProjectFullPath};
                            return (false, projectsInCycle);
                        }
                    }
                }
                else
                {
                    // a new project that has to be evaluated
                    var loadReference = LoadGraph(projectReferenceConfigurationMetadata, nodeState, projectCollection,
                        globalProperties);
                    if (!loadReference.success)
                    {
                        if (loadReference.projectsInCycle[0].Equals(parsedProject.Project.FullPath))
                        {
                            // we have reached the nth project in the cycle, form error message and throw
                            loadReference.projectsInCycle.Add(projectReferenceConfigurationMetadata.ProjectFullPath);
                            var errorMessage = new StringBuilder(500);
                            for (int i = loadReference.projectsInCycle.Count-1; i >= 0; i--)
                            {
                                errorMessage.Append(loadReference.projectsInCycle[i]).AppendLine();
                            }

                            throw new CircularDependencyException(string.Format(
                                ResourceUtilities.GetResourceString("CircularDependencyInProjectGraph"),
                                errorMessage));
                        }
                        else
                        {
                            loadReference.projectsInCycle.Add(projectReferenceConfigurationMetadata.ProjectFullPath);
                            return (false, loadReference.projectsInCycle);
                        }
                    }
                }

                parsedProject.AddProjectReference(_allParsedProjects[projectReferenceConfigurationMetadata]);
            }

            nodeState[projectToEvaluate] = NodeState.Processed;
            return (true, null);
        }

        private static ImmutableList<string> DetermineTargetsToPropagate(ProjectGraphNode node, ImmutableList<string> entryTargets)
        {
            var targetsToPropagate = ImmutableList<string>.Empty;
            ICollection<ProjectItemInstance> projectReferenceTargets = node.Project.GetItems(ProjectReferenceTargetsItemType);
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
