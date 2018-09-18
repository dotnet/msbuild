// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;

namespace Microsoft.Build.Graph
{
    /// <summary>
    /// Represents a graph of evaluated projects.
    /// </summary>
    public sealed class ProjectGraph
    {
        private const string ProjectReferenceItemName = "ProjectReference";
        private const string FullPathMetadataName = "FullPath";
        private const string ProjectReferenceTargetsItemType = "ProjectReferenceTargets";
        private const string ProjectReferenceTargetsMetadataName = "Targets";
        private const string DefaultTargetsMarker = ".default";

        private readonly Dictionary<string, ProjectGraphNode> _allParsedProjects =
            new Dictionary<string, ProjectGraphNode>(StringComparer.OrdinalIgnoreCase);

        // TODO: We probably want to mirror all relevant constructor overloads from Project

        /// <summary>
        /// Constructs a graph starting from the given project file.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <exception cref="InvalidProjectFileException">If the evaluation of any project in the graph fails.</exception>
        public ProjectGraph(string entryProjectFile)
            : this(entryProjectFile, ProjectCollection.GlobalProjectCollection, null, null)
        {
        }

        /// <summary>
        /// Constructs a graph starting from the given project file.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <param name="projectCollection">The collection with which all projects in the graph should be associated. May not be null.</param>
        /// <param name="globalProperties">The global properties to use for all projects. May be null, in which case the containing project collection's global properties will be used.</param>
        /// <param name="toolsVersion">The tools version. May be null.</param>
        /// <exception cref="InvalidProjectFileException">If the evaluation of any project in the graph fails.</exception>
        public ProjectGraph(
            string entryProjectFile,
            ProjectCollection projectCollection,
            Dictionary<string, string> globalProperties,
            string toolsVersion)
        {
            LoadGraph(entryProjectFile, projectCollection, globalProperties, toolsVersion);
            EntryProjectNode = _allParsedProjects[entryProjectFile];
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
            string projectFilePath,
            ProjectCollection projectCollection,
            Dictionary<string, string> globalProperties,
            string toolsVersion)
        {
            var project = new ProjectInstance(projectFilePath, globalProperties, toolsVersion, projectCollection);
            var graphNode = new ProjectGraphNode(project);
            _allParsedProjects.Add(projectFilePath, graphNode);
            return graphNode;
        }

        /// <summary>
        /// Load a graph with root node at entryProjectFile
        /// Maintain a queue of projects to be processed- each queue item is a key value pair of the project to be evaluated and its parent
        /// Once the project has been evaluated, add a project reference to this evaluated target from the parent node
        /// </summary>
        private void LoadGraph(string entryProjectFile, ProjectCollection projectCollection, Dictionary<string, string> globalProperties, string toolsVersion)
        {
            var projectsToEvaluate = new Queue<KeyValuePair<string, ProjectGraphNode>>();
            // entry project node has no parent
            projectsToEvaluate.Enqueue(new KeyValuePair<string, ProjectGraphNode>(entryProjectFile, null));
            while (projectsToEvaluate.Count != 0)
            {
                KeyValuePair<string, ProjectGraphNode> projectToEvaluateAndParentNode = projectsToEvaluate.Dequeue();
                string projectToEvaluate = projectToEvaluateAndParentNode.Key;
                if (!_allParsedProjects.TryGetValue(projectToEvaluate, out ProjectGraphNode parsedProject))
                {
                    parsedProject = CreateNewNode(projectToEvaluate, projectCollection, globalProperties, toolsVersion);
                    IEnumerable<ProjectItemInstance> projectReferenceItems = parsedProject.Project.GetItems(ProjectReferenceItemName);
                    foreach (var projectReferenceToParse in projectReferenceItems)
                    {
                        string projectReferencePath = projectReferenceToParse.GetMetadataValue(FullPathMetadataName);
                        projectsToEvaluate.Enqueue(new KeyValuePair<string, ProjectGraphNode>(projectReferencePath, parsedProject));
                    }
                }

                if (projectToEvaluateAndParentNode.Value != null)
                {
                    ProjectGraphNode parentNode = projectToEvaluateAndParentNode.Value;
                    parentNode.AddProjectReference(parsedProject);
                }
            }
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
