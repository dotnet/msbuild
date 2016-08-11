// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#region Using directives

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.BuildEngine.Shared;

#endregion

namespace Microsoft.Build.BuildEngine
{
    internal sealed class ProjectManager
    {
        #region Constructors
        /// <summary>
        /// Default constructor.  Just instantiates the hash table.
        /// </summary>
        /// <owner>RGoel</owner>
        internal ProjectManager()
        {
            this.projects = new Hashtable(StringComparer.OrdinalIgnoreCase);
            this.nodeToProjectsMapping = new Hashtable(StringComparer.OrdinalIgnoreCase);
            this.unloadedProjects = new Hashtable(StringComparer.OrdinalIgnoreCase);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Adds the specified Project object to our data structure, if it's not already present.
        /// </summary>
        /// <param name="project"></param>
        internal void AddProject(Project project)
        {
            // We should never be asked to store a nameless project in our list.
            ErrorUtilities.VerifyThrow(project.FullFileName.Length > 0, "Can't store nameless projects");

            AddProject(projects, project);
        }

        /// <summary>
        /// Removes all projects with the specified full path from our manager.
        /// </summary>
        /// <param name="fullPath"></param>
        internal void RemoveProjects
            (
            string fullPath
            )
        {
            this.projects.Remove(fullPath);
        }

        /// <summary>
        /// Searches our tables for a project with same full path, tools version, and global property settings 
        /// Removes particular project from the project manager.
        /// </summary>
        /// <param name="project"></param>
        internal void RemoveProject
            (
            Project project
            )
        {
            // We should never be asked to remove null project
            ErrorUtilities.VerifyThrow(project != null, "Shouldn't ask to remove null projects");

            // See if there's an entry in our table for this particular full path.
            ArrayList projectsWithThisFullPath = (ArrayList)this.projects[project.FullFileName];

            // The project should be in the table
            ErrorUtilities.VerifyThrow(projectsWithThisFullPath != null, "Project missing from the list");

            int project_index = -1;
            for (int i = 0; i < projectsWithThisFullPath.Count; i++)
            {
                if (projectsWithThisFullPath[i] == project)
                {
                    project_index = i;
                }
            }
            
            // The project should be in the table
            ErrorUtilities.VerifyThrow(project_index != -1, "Project missing from the list");

            if (project_index != -1)
            {
                projectsWithThisFullPath.RemoveAt(project_index);
                AddUnloadedProjectRecord(project.FullFileName, project.GlobalProperties, project.ToolsVersion);
            }
        }

        /// <summary>
        /// Searches our tables for a project with same full path and global property settings 
        /// as those passed in to the method.  
        /// </summary>
        /// <param name="projectFileFullPath"></param>
        /// <param name="globalProperties"></param>
        /// <param name="toolsVersion">Tools version a matching project must have</param>
        /// <returns>Project object if found, null otherwise.</returns>
        internal Project GetProject
            (
            string projectFileFullPath,
            BuildPropertyGroup globalProperties,
            string toolsVersion
            )
        {
            Project project = GetProject(projects, projectFileFullPath, globalProperties, toolsVersion);

            return project;
        }

        /// <summary>
        /// Searches our tables for a project with same project id
        /// as the one passed in to the method. Note this method is currently O(n) 
        /// with the number of projects, so if it used on a hot code path it needs to 
        /// use an extra hashtable to achieve O(1).
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns>Project object if found, null otherwise.</returns>
        internal Project GetProject
            (
            int projectId
            )
        {
            // Loop through them and find the one with the matching id.
            foreach (DictionaryEntry entry in projects)
            {
                ArrayList projectsWithThisFullPath = (ArrayList)entry.Value;
                foreach (Project candidateProject in projectsWithThisFullPath)
                {
                    if (candidateProject.Id == projectId)
                    {
                        return candidateProject;
                    }
                }
            }

            // No project was found that matched the id specified.
            return null;
        }

        /// <summary>
        /// Gets the first project contained in the ProjectManager that matches the full path
        /// specified.
        /// </summary>
        /// <param name="projectFileFullPath"></param>
        /// <returns></returns>
        internal Project GetFirstProject
            (
            string projectFileFullPath
            )
        {
            // Get the list of projects that have this full path.
            ArrayList projectsWithThisFullPath = (ArrayList) this.projects[projectFileFullPath];

            if ((projectsWithThisFullPath != null) && (projectsWithThisFullPath.Count > 0))
            {
                return (Project) projectsWithThisFullPath[0];
            }

            // No project was found that matched the full path specified.
            return null;
        }

        /// <summary>
        /// Gets the list of projects which are currently in process of being build (i.e have at least
        /// one build request inside the project)
        /// </summary>
        /// <returns>List of in progress projects</returns>
        internal List<Project> GetInProgressProjects()
        {
            List<Project> inProgressProjects = new List<Project>();
            foreach (DictionaryEntry entry in projects)
            {
                ArrayList projectsWithThisFullPath = (ArrayList)entry.Value;
                foreach (Project candidateProject in projectsWithThisFullPath)
                {
                    if (candidateProject.IsBuilding)
                    {
                        inProgressProjects.Add(candidateProject);
                    }
                }
            }
            return inProgressProjects;
        }

        /// <summary>
        /// Resets the build status of every single project in our ProjectManager.
        /// </summary>
        internal void ResetBuildStatusForAllProjects
            (
            )
        {
            // Iterate over every single project in our data structures, and reset them all.
            foreach (ArrayList projectList in this.projects.Values)
            {
                foreach (Project project in projectList)
                {
                    project.ResetBuildStatus();
                }
            }
            // Since the status is reset for all projects it is no longer relevant if the project was loaded before
            this.unloadedProjects.Clear();
        }

        /// <summary>
        /// Clears all references to all projects from this ProjectManager.
        /// </summary>
        internal void Clear
            (
            )
        {
            this.projects.Clear();
            this.nodeToProjectsMapping.Clear();
            this.unloadedProjects.Clear();
        }

        #region Methods managing the mapping between project and remote nodes

        /// <summary>
        /// Store a record indicating that project with the given name is assigned to the given node,
        /// it's not already present.
        /// </summary>
        internal void AddRemoteProject
        (
            string projectFileFullPath,
            BuildPropertyGroup globalProperties,
            string toolsVersion,
            int nodeIndex
        )
        {
            ErrorUtilities.VerifyThrow(nodeIndex != EngineCallback.parentNode, "Should not try to insert nodeIndex of parentNode");
            AddProjectEntry(nodeToProjectsMapping, projectFileFullPath, globalProperties, toolsVersion, nodeIndex);
        }

        /// <summary>
        /// Get a node that the project has been assigned to
        /// </summary>
        /// <returns>Index of the node the project is assigned to and 0 otherwise</returns>
        internal int GetRemoteProject
        (
            string projectFileFullPath,
            BuildPropertyGroup globalProperties,
            string toolsVersion
        )
        {
            ProjectEntry projectEntry = GetProjectEntry(nodeToProjectsMapping, projectFileFullPath, globalProperties, toolsVersion);
            if (projectEntry != null)
            {
                return projectEntry.nodeIndex;
            }
            else
            {
                return EngineCallback.invalidNode;
            }
        }
        #endregion

        #region Methods managing the record of unloaded projects

        /// <summary>
        /// This function adds the project to the table of previously loaded projects, if it's 
        /// not already present.
        /// </summary>
        private void AddUnloadedProjectRecord
        (
            string projectFileFullPath,
            BuildPropertyGroup globalProperties,
            string toolsVersion
        )
        {
            AddProjectEntry(unloadedProjects, projectFileFullPath, globalProperties, toolsVersion, EngineCallback.invalidNode /* node index not needed */);
        }

        /// <summary>
        /// This functions returns true if a project with the same properties, toolset version and filename has been previously loaded. It
        /// will return false for currently loaded projects and projects that have never been loaded.
        /// </summary>
        /// <returns>True if exact same instance has been loaded before </returns>
        internal bool HasProjectBeenLoaded
        (
            string projectFileFullPath,
            BuildPropertyGroup globalProperties,
            string toolsVersion
        )
        {
            ProjectEntry projectEntry = GetProjectEntry(unloadedProjects, projectFileFullPath, globalProperties, toolsVersion);
            if (projectEntry != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Adds a project to the specified table, if it isn't already present.
        /// </summary>
        internal static void AddProject(Hashtable projectTable, Project project)
        {
            Project existingEntry = GetProject(projectTable, project.FullFileName, project.GlobalProperties, project.ToolsVersion);

            if (existingEntry != null)
            {
                // We already have this entry
                return;
            }

            // See if there's an entry in our table for this particular full path.
            ArrayList projectsWithThisFullPath = (ArrayList)projectTable[project.FullFileName];

            // If not, create one.  The "value" in the Hashtable is an ArrayList of projects.
            if (projectsWithThisFullPath == null)
            {
                projectsWithThisFullPath = new ArrayList();
                projectTable[project.FullFileName] = projectsWithThisFullPath;
            }

            // Add the specified project to the ArrayList of projects for this particular full path.
            projectsWithThisFullPath.Add(project);
        }

        /// <summary>
        /// Add a project entry to the specified table, if it isn't already present.
        /// </summary>
        internal static void AddProjectEntry(Hashtable projectEntryTable, string projectFileFullPath, BuildPropertyGroup globalProperties, string toolsVersion, int nodeIndex)
        {
            ProjectEntry existingEntry = GetProjectEntry(projectEntryTable, projectFileFullPath, globalProperties, toolsVersion);

            if (existingEntry != null)
            {
                ErrorUtilities.VerifyThrow(existingEntry.nodeIndex == nodeIndex, "nodeIndex should match existing ProjectEntry");
                // We already have this entry
                return;
            }

            // See if there's an entry in our table for this particular full path.
            ArrayList projectsWithThisFullPath = (ArrayList)projectEntryTable[projectFileFullPath];

            // If not, create one.  The "value" in the Hashtable is an ArrayList of projects.
            if (projectsWithThisFullPath == null)
            {
                projectsWithThisFullPath = new ArrayList();
                projectEntryTable[projectFileFullPath] = projectsWithThisFullPath;
            }

            ProjectEntry projectEntry = new ProjectEntry();
            projectEntry.toolsVersion = toolsVersion;
            projectEntry.globalProperties = globalProperties;
            projectEntry.nodeIndex = nodeIndex;
            // Break up the link to the project to avoid keeping it in memory
            projectEntry.globalProperties.ClearParentProject();

            projectsWithThisFullPath.Add(projectEntry);
        }

        /// <summary>
        /// Retrieve any project from the table that has the same file name, global properties, and tools version.
        /// </summary>
        internal static Project GetProject(Hashtable table, string projectFileFullPath, BuildPropertyGroup globalProperties, string toolsVersion)
        {
            // Get the list of projects that have this full path.
            ArrayList projectsWithThisFullPath = (ArrayList)table[projectFileFullPath];

            if (projectsWithThisFullPath != null)
            {
                // Loop through them and find the one with the matching set of global properties.
                foreach (Project candidateProject in projectsWithThisFullPath)
                {
                    if (candidateProject.IsEquivalentToProject(projectFileFullPath, globalProperties, toolsVersion))
                    {
                        return candidateProject;
                    }
                }
            }

            // No project was found that matched the full path and the global properties specified.
            return null;
        }

        /// <summary>
        /// Retrieve the project entry from the entry table based on project file name, globalProperties, and toolsVersion. 
        /// </summary>
        internal static ProjectEntry GetProjectEntry(Hashtable entryTable, string projectFileFullPath, BuildPropertyGroup globalProperties, string toolsVersion)
        {
            // Get the list of projects that have this full path.
            ArrayList projectsWithFullPath = (ArrayList)entryTable[projectFileFullPath];

            if (projectsWithFullPath != null)
            {
                // Loop through them and find the one with the matching set of global properties.
                foreach (ProjectEntry projectEntry in projectsWithFullPath)
                {
                    if ((String.Compare(projectEntry.toolsVersion, toolsVersion, StringComparison.OrdinalIgnoreCase) == 0) &&
                        projectEntry.globalProperties.IsEquivalent(globalProperties))
                    {
                        return projectEntry;
                    }
                }
            }
            return null;
        }
        #endregion
        #endregion

        #region Data
        // This hash table tracks all the projects that are currently building,
        // or are being kept around from the last build for perf reasons (so
        // we don't have to reload the same projects over and over in IDE 
        // scenarios.
        // The key for this hash table is the case-insensitive full path to the
        // project file.  The value in this hash table is an ArrayList of Project
        // objects that came from that full path.  The reason there could be
        // multiple Project objects with the same full path is because they 
        // may each be using a different set of global properties, and we can't
        // have them tromp on each other.
        private Hashtable projects;
        // Once the project is loaded on the remote node, all versions of the project
        // will be loaded and processed on the same node to reuse the XML. This table
        // stores a record of between nodes and projects
        private Hashtable nodeToProjectsMapping;
        // Table of projects that have been unloaded during the current build
        private Hashtable unloadedProjects;
        #endregion

        #region Helper class 
        internal class ProjectEntry
        {
            internal BuildPropertyGroup globalProperties;
            internal string toolsVersion;
            internal int nodeIndex;
        }
        
        #endregion
    }
}
