// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
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
    internal sealed class ProjectGraph
    {
        private const string ProjectReferenceString = "ProjectReference";
        private const string FullPathString = "FullPath";

        private Dictionary<string, ProjectGraphNode> _allParsedProjects = new Dictionary<string, ProjectGraphNode>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Constructs a graph starting from the given project file.
        /// </summary>
        /// <param name="entryProjectFile">The project file to use as the entry point in constructing the graph</param>
        /// <exception cref="InvalidProjectFileException">If the evaluation of any project in the graph fails.</exception>
        public ProjectGraph(string entryProjectFile)
        {
            AddNewNode(entryProjectFile);
        }

        /// <summary>
        /// Constructs a graph starting from the given project files.
        /// </summary>
        /// <param name="entryProjectFiles">The project files to use as the entry points in constructing the graph</param>
        /// <exception cref="InvalidProjectFileException">If the evaluation of any project in the graph fails.</exception>
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
