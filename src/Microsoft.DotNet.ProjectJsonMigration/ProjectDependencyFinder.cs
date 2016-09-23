// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using Microsoft.DotNet.Tools.Common;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class ProjectDependencyFinder 
    {
        public IEnumerable<ProjectDependency> ResolveProjectDependencies(string projectDir, string xprojFile = null)
        {
            var projectContexts = ProjectContext.CreateContextForEachFramework(projectDir);
            xprojFile = xprojFile ?? FindXprojFile(projectDir);

            ProjectRootElement xproj = null;
            if (xprojFile != null)
            {
                xproj = ProjectRootElement.Open(xprojFile);
            }

            return ResolveProjectDependencies(projectContexts, ResolveXProjProjectDependencyNames(xproj));
        }

        public IEnumerable<ProjectDependency> ResolveProjectDependencies(
            IEnumerable<ProjectContext> projectContexts,
            IEnumerable<string> preResolvedProjects=null)
        {
            foreach(var projectContext in projectContexts)
            {
                foreach(var projectDependency in ResolveProjectDependencies(projectContext, preResolvedProjects))
                {
                    yield return projectDependency;
                }
            }
        }

        public IEnumerable<ProjectDependency> ResolveProjectDependencies(
            ProjectContext projectContext,
            IEnumerable<string> preResolvedProjects=null)
        {
            preResolvedProjects = preResolvedProjects ?? new HashSet<string>();

            var projectExports = projectContext.CreateExporter("_").GetDependencies();
            var possibleProjectDependencies = 
                FindPossibleProjectDependencies(projectContext.ProjectFile.ProjectFilePath);

            var projectDependencies = new List<ProjectDependency>();
            foreach (var projectExport in projectExports)
            {
                var projectExportName = projectExport.Library.Identity.Name;
                ProjectDependency projectDependency;

                if (!possibleProjectDependencies.TryGetValue(projectExportName, out projectDependency))
                {
                    if (projectExport.Library.Identity.Type.Equals(LibraryType.Project) 
                        && !preResolvedProjects.Contains(projectExportName))
                    {
                        MigrationErrorCodes
                            .MIGRATE1014($"Unresolved project dependency ({projectExportName})").Throw();
                    }
                    else
                    {
                        continue;
                    }
                }

                projectDependencies.Add(projectDependency);
            }

            return projectDependencies;
        }

        private IEnumerable<string> ResolveXProjProjectDependencyNames(ProjectRootElement xproj)
        {
            var xprojDependencies = ResolveXProjProjectDependencies(xproj).SelectMany(r => r.Includes());
            return new HashSet<string>(xprojDependencies.Select(p => Path.GetFileNameWithoutExtension(
                                                                          PathUtility.GetPathWithDirectorySeparator(p))));
        }

        internal IEnumerable<ProjectItemElement> ResolveXProjProjectDependencies(ProjectRootElement xproj)
        {
            if (xproj == null)
            {
                MigrationTrace.Instance.WriteLine($"{nameof(ProjectDependencyFinder)}: No xproj file given.");
                return Enumerable.Empty<ProjectItemElement>();
            }

            return xproj.Items
                        .Where(i => i.ItemType == "ProjectReference")
                        .Where(p => p.Includes().Any(
                                    include => string.Equals(Path.GetExtension(include), ".csproj", StringComparison.OrdinalIgnoreCase)));
        }

        internal string FindXprojFile(string projectDirectory)
        {
            var allXprojFiles = Directory.EnumerateFiles(projectDirectory, "*.xproj", SearchOption.TopDirectoryOnly);

            if (allXprojFiles.Count() > 1)
            {
                MigrationErrorCodes
                    .MIGRATE1017($"Multiple xproj files found in {projectDirectory}, please specify which to use")
                    .Throw();
            }

            return allXprojFiles.FirstOrDefault();
        }

        private Dictionary<string, ProjectDependency> FindPossibleProjectDependencies(string projectJsonFilePath)
        {
            var projectRootDirectory = GetRootFromProjectJson(projectJsonFilePath);

            var projectSearchPaths = new List<string>();
            projectSearchPaths.Add(projectRootDirectory);

            var globalPaths = GetGlobalPaths(projectRootDirectory);
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
        /// Finds the parent directory of the project.json.
        /// </summary>
        /// <param name="projectJsonPath">Full path to project.json.</param>
        private static string GetRootFromProjectJson(string projectJsonPath)
        {
            if (!string.IsNullOrEmpty(projectJsonPath))
            {
                var file = new FileInfo(projectJsonPath);

                // If for some reason we are at the root of the drive this will be null
                // Use the file directory instead.
                if (file.Directory.Parent == null)
                {
                    return file.Directory.FullName;
                }
                else
                {
                    return file.Directory.Parent.FullName;
                }
            }

            return projectJsonPath;
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

        private static string ResolveRootDirectory(string projectPath)
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
