// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class ProjectDependencyFinder 
    {
        public Dictionary<string, ProjectDependency> FindPossibleProjectDependencies(string projectJsonFilePath)
        {
            var projectDirectory = Path.GetDirectoryName(projectJsonFilePath);

            var projectSearchPaths = new List<string>();
            projectSearchPaths.Add(projectDirectory);

            var globalPaths = GetGlobalPaths(projectDirectory);
            projectSearchPaths = projectSearchPaths.Union(globalPaths).ToList();

            var projects = new Dictionary<string, ProjectDependency>(StringComparer.Ordinal);

            foreach (var project in GetPotentialProjects(projectSearchPaths))
            {
                if (projects.ContainsKey(project.Name))
                {
                    // Remove the existing project if it doesn't have project.json
                    // project.json isn't checked until the project is resolved, but here
                    // we need to check it up front.
                    var otherProject = projects[project.Name];

                    if (project.ProjectFilePath != otherProject.ProjectFilePath)
                    {
                        var projectExists = File.Exists(project.ProjectFilePath);
                        var otherExists = File.Exists(otherProject.ProjectFilePath);

                        if (projectExists != otherExists
                            && projectExists
                            && !otherExists)
                        {
                            // the project currently in the cache does not exist, but this one does
                            // remove the old project and add the current one
                            projects[project.Name] = project;
                        }
                    }
                }
                else
                {
                    projects.Add(project.Name, project);
                }
            }

            return projects;
        }

        /// <summary>
        /// Create the list of potential projects from the search paths.
        /// </summary>
        private static List<ProjectDependency> GetPotentialProjects(IEnumerable<string> searchPaths)
        {
            var projects = new List<ProjectDependency>();

            // Resolve all of the potential projects
            foreach (var searchPath in searchPaths)
            {
                var directory = new DirectoryInfo(searchPath);

                if (!directory.Exists)
                {
                    continue;
                }

                foreach (var projectDirectory in directory.EnumerateDirectories())
                {
                    // Create the path to the project.json file.
                    var projectFilePath = Path.Combine(projectDirectory.FullName, "project.json");

                    // We INTENTIONALLY do not do an exists check here because it requires disk I/O
                    // Instead, we'll do an exists check when we try to resolve

                    // Check if we've already added this, just in case it was pre-loaded into the cache
                    var project = new ProjectDependency(projectDirectory.Name, projectFilePath);

                    projects.Add(project);
                }
            }

            return projects;
        }

        private static List<string> GetGlobalPaths(string rootPath)
        {
            var paths = new List<string>();

            var globalJsonRoot = ResolveRootDirectory(rootPath);

            GlobalSettings globalSettings;
            if (GlobalSettings.TryGetGlobalSettings(globalJsonRoot, out globalSettings))
            {
                foreach (var sourcePath in globalSettings.ProjectPaths)
                {
                    var path = Path.GetFullPath(Path.Combine(globalJsonRoot, sourcePath));

                    paths.Add(path);
                }
            }

            return paths;
        }

        public static string ResolveRootDirectory(string projectPath)
        {
            var di = new DirectoryInfo(projectPath);

            while (di.Parent != null)
            {
                var globalJsonPath = Path.Combine(di.FullName, GlobalSettings.GlobalFileName);

                if (File.Exists(globalJsonPath))
                {
                    return di.FullName;
                }

                di = di.Parent;
            }

            // If we don't find any files then make the project folder the root
            return projectPath;
        }

        private class GlobalSettings
        {
            public const string GlobalFileName = "global.json";
            public IList<string> ProjectPaths { get; private set; }
            public string PackagesPath { get; private set; }
            public string FilePath { get; private set; }

            public string RootPath
            {
                get { return Path.GetDirectoryName(FilePath); }
            }

            public static bool TryGetGlobalSettings(string path, out GlobalSettings globalSettings)
            {
                globalSettings = null;

                string globalJsonPath = null;

                if (Path.GetFileName(path) == GlobalFileName)
                {
                    globalJsonPath = path;
                    path = Path.GetDirectoryName(path);
                }
                else if (!HasGlobalFile(path))
                {
                    return false;
                }
                else
                {
                    globalJsonPath = Path.Combine(path, GlobalFileName);
                }

                globalSettings = new GlobalSettings();

                try
                {
                    var json = File.ReadAllText(globalJsonPath);

                    JObject settings = JObject.Parse(json);

                    var projects = settings["projects"];
                    var dependencies = settings["dependencies"] as JObject;

                    globalSettings.ProjectPaths = projects == null ? new string[] { } : projects.Select(a => a.Value<string>()).ToArray();;
                    globalSettings.PackagesPath = settings.Value<string>("packages");
                    globalSettings.FilePath = globalJsonPath;
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, globalJsonPath);
                }

                return true;
            }

            public static bool HasGlobalFile(string path)
            {
                var projectPath = Path.Combine(path, GlobalFileName);

                return File.Exists(projectPath);
            }
        }
    }
}
