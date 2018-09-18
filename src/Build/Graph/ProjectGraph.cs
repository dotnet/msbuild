// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;

namespace Microsoft.Build.Graph
{
    /// <summary>
    /// Represents a graph of evaluated projects.
    /// </summary>
    internal sealed class ProjectGraph
    {
        private const string ProjectReferenceString = "ProjectReference";
        private const string FullPathString = "FullPath";

        private readonly Dictionary<string, ProjectGraphNode> _allParsedProjects =
            new Dictionary<string, ProjectGraphNode>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Constructs a graph starting from the given project file.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <exception cref="InvalidProjectFileException">If the evaluation of any project in the graph fails.</exception>
        public ProjectGraph(string entryProjectFile)
        {
            LoadGraph(entryProjectFile);
        }

        /// <summary>
        /// Get an unordered collection of all project nodes in the graph.
        /// </summary>
        public IReadOnlyCollection<ProjectGraphNode> ProjectNodes => _allParsedProjects.Values;

        private ProjectGraphNode CreateNewNode(string projectFilePath)
        {
            var graphNode =
                new ProjectGraphNode(ProjectCollection.GlobalProjectCollection.LoadProject(projectFilePath));
            _allParsedProjects.Add(projectFilePath, graphNode);
            return graphNode;
        }

        /// <summary>
        /// Load a graph with root node at entryProjectFile
        /// Maintain a queue of projects to be processed- each queue item is a key value pair of the project to be evaluated and its parent
        /// Once the project has been evaluated, add a project reference to this evaluated target from the parent node
        /// </summary>
        /// <param name="entryProjectFile"></param>
        private void LoadGraph(string entryProjectFile)
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
                    parsedProject = CreateNewNode(projectToEvaluate);
                    IEnumerable<ProjectItem> projectReferenceItems =
                        parsedProject.Project.GetItems(ProjectReferenceString);
                    foreach (var projectReferenceToParse in projectReferenceItems)
                    {
                        string projectReferencePath = projectReferenceToParse.GetMetadataValue(FullPathString);
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

    }
}
