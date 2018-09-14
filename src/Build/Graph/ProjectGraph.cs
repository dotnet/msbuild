// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
        private const string ProjectReferenceString = "ProjectReference";
        private const string FullPathString = "FullPath";

        private Dictionary<string, ProjectGraphNode> _allParsedProjects = new Dictionary<string, ProjectGraphNode>(StringComparer.OrdinalIgnoreCase);

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
            var project = projectCollection.LoadProject(entryProjectFile, globalProperties, toolsVersion);
            EntryProjectNode = new ProjectGraphNode(project);
            _projectNodes.Add(EntryProjectNode);
        }

        /// <summary>
        /// Gets the project node representing the entry point.
        /// </summary>
        public ProjectGraphNode EntryProjectNode { get; }

        public ProjectGraph(IEnumerable<string> entryProjectFiles)
        {
            var projectsToParse = new Queue<string>();
            foreach (var entryProjectFile in entryProjectFiles)
            {
                projectsToParse.Enqueue(entryProjectFile);
            }

            LoadGraph(projectsToParse);
        }

        /// <summary>
        /// Get an unordered collection of all project nodes in the graph.
        /// </summary>
        public IReadOnlyCollection<ProjectGraphNode> ProjectNodes => _allParsedProjects.Values;

        private ProjectGraphNode AddNewNode(string projectFilePath)
        {
            var graphNode =
                new ProjectGraphNode(ProjectCollection.GlobalProjectCollection.LoadProject(projectFilePath));
            _allParsedProjects.Add(projectFilePath, graphNode);
            return graphNode;
        }

        private List<ProjectGraphNode> LoadGraph(Queue<string> projectsToParse)
        {
            var parsedProjects = new List<ProjectGraphNode>();
            while (projectsToParse.Count != 0)
            {
                string projectToParse = projectsToParse.Dequeue();
                if (_allParsedProjects.TryGetValue(projectToParse, out ProjectGraphNode parsedProject))
                {
                    parsedProjects.Add(parsedProject);
                }
                else
                {
                    parsedProject = AddNewNode(projectToParse);
                    parsedProjects.Add(parsedProject);
                    ProjectInstance projectInstance = parsedProject.Project.CreateProjectInstance();
                    IEnumerable<ProjectItemInstance> projectReferenceItems = projectInstance.GetItems(ProjectReferenceString);
                    var projectReferencesToParse = new Queue<string>();
                    if (projectReferenceItems != null && projectReferenceItems.Any())
                    {
                        foreach (var projectReferenceToParse in projectReferenceItems)
                        {
                            string projectReferencePath = projectReferenceToParse.GetMetadataValue(FullPathString);
                            projectReferencesToParse.Enqueue(projectReferencePath);
                        }
                        parsedProject.AddProjectReferences(LoadGraph(projectReferencesToParse));
                    }
                }
            }

            return parsedProjects;
        }
  
    }
}
