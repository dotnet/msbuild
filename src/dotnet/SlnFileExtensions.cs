// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Common
{
    internal static class SlnFileExtensions
    {
        public static void AddProject(this SlnFile slnFile, string fullProjectPath)
        {
            if (string.IsNullOrEmpty(fullProjectPath))
            {
                throw new ArgumentException();
            }

            var relativeProjectPath = Path.GetRelativePath(
                PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory),
                fullProjectPath);

            if (slnFile.Projects.Any((p) =>
                    string.Equals(p.FilePath, relativeProjectPath, StringComparison.OrdinalIgnoreCase)))
            {
                Reporter.Output.WriteLine(string.Format(
                    CommonLocalizableStrings.SolutionAlreadyContainsProject,
                    slnFile.FullPath,
                    relativeProjectPath));
            }
            else
            {
                ProjectInstance projectInstance = null;
                try
                {
                    projectInstance = new ProjectInstance(fullProjectPath);
                }
                catch (InvalidProjectFileException e)
                {
                    Reporter.Error.WriteLine(string.Format(
                        CommonLocalizableStrings.InvalidProjectWithExceptionMessage,
                        fullProjectPath,
                        e.Message));
                    return;
                }

                var slnProject = new SlnProject
                {
                    Id = projectInstance.GetProjectId(),
                    TypeGuid = projectInstance.GetProjectTypeGuid(),
                    Name = Path.GetFileNameWithoutExtension(relativeProjectPath),
                    FilePath = relativeProjectPath
                };

                slnFile.AddDefaultBuildConfigurations(slnProject);

                slnFile.AddSolutionFolders(slnProject);

                slnFile.Projects.Add(slnProject);

                Reporter.Output.WriteLine(
                    string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, relativeProjectPath));
            }
        }

        public static void AddDefaultBuildConfigurations(this SlnFile slnFile, SlnProject slnProject)
        {
            if (slnProject == null)
            {
                throw new ArgumentException();
            }

            var defaultConfigurations = new List<string>()
            {
                "Debug|Any CPU",
                "Debug|x64",
                "Debug|x86",
                "Release|Any CPU",
                "Release|x64",
                "Release|x86",
            };

            // NOTE: The order you create the sections determines the order they are written to the sln
            // file. In the case of an empty sln file, in order to make sure the solution configurations
            // section comes first we need to add it first. This doesn't affect correctness but does 
            // stop VS from re-ordering things later on. Since we are keeping the SlnFile class low-level
            // it shouldn't care about the VS implementation details. That's why we handle this here.
            AddDefaultSolutionConfigurations(defaultConfigurations, slnFile.SolutionConfigurationsSection);
            AddDefaultProjectConfigurations(
                defaultConfigurations,
                slnFile.ProjectConfigurationsSection.GetOrCreatePropertySet(slnProject.Id));
        }

        private static void AddDefaultSolutionConfigurations(
            List<string> defaultConfigurations,
            SlnPropertySet solutionConfigs)
        {
            foreach (var config in defaultConfigurations)
            {
                if (!solutionConfigs.ContainsKey(config))
                {
                    solutionConfigs[config] = config;
                }
            }
        }

        private static void AddDefaultProjectConfigurations(
            List<string> defaultConfigurations,
            SlnPropertySet projectConfigs)
        {
            foreach (var config in defaultConfigurations)
            {
                var activeCfgKey = $"{config}.ActiveCfg";
                if (!projectConfigs.ContainsKey(activeCfgKey))
                {
                    projectConfigs[activeCfgKey] = config;
                }

                var build0Key = $"{config}.Build.0";
                if (!projectConfigs.ContainsKey(build0Key))
                {
                    projectConfigs[build0Key] = config;
                }
            }
        }

        public static void AddSolutionFolders(this SlnFile slnFile, SlnProject slnProject)
        {
            if (slnProject == null)
            {
                throw new ArgumentException();
            }

            var solutionFolders = slnProject.GetSolutionFoldersFromProject();

            if (solutionFolders.Any())
            {
                var nestedProjectsSection = slnFile.Sections.GetOrCreateSection(
                    "NestedProjects",
                    SlnSectionType.PreProcess);

                var pathToGuidMap = slnFile.GetSolutionFolderPaths(nestedProjectsSection.Properties);

                string parentDirGuid = null;
                var solutionFolderHierarchy = string.Empty;
                foreach (var dir in solutionFolders)
                {
                    solutionFolderHierarchy = Path.Combine(solutionFolderHierarchy, dir);
                    if (pathToGuidMap.ContainsKey(solutionFolderHierarchy))
                    {
                        parentDirGuid = pathToGuidMap[solutionFolderHierarchy];
                    }
                    else
                    {
                        var solutionFolder = new SlnProject
                        {
                            Id = Guid.NewGuid().ToString("B").ToUpper(),
                            TypeGuid = ProjectTypeGuids.SolutionFolderGuid,
                            Name = dir,
                            FilePath = dir
                        };

                        slnFile.Projects.Add(solutionFolder);

                        if (parentDirGuid != null)
                        {
                            nestedProjectsSection.Properties[solutionFolder.Id] = parentDirGuid;
                        }
                        parentDirGuid = solutionFolder.Id;
                    }
                }

                nestedProjectsSection.Properties[slnProject.Id] = parentDirGuid;
            }
        }

        private static IDictionary<string, string> GetSolutionFolderPaths(
            this SlnFile slnFile,
            SlnPropertySet nestedProjects)
        {
            var solutionFolderPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var solutionFolderProjects = slnFile.Projects.GetProjectsByType(ProjectTypeGuids.SolutionFolderGuid);
            foreach (var slnProject in solutionFolderProjects)
            {
                var path = slnProject.FilePath;
                var id = slnProject.Id;
                while (nestedProjects.ContainsKey(id))
                {
                    id = nestedProjects[id];
                    var parentSlnProject = solutionFolderProjects.Where(p => p.Id == id).Single();
                    path = Path.Combine(parentSlnProject.FilePath, path);
                }

                solutionFolderPaths[path] = slnProject.Id;
            }

            return solutionFolderPaths;
        }

        public static bool RemoveProject(this SlnFile slnFile, string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                throw new ArgumentException();
            }

            var projectPathNormalized = PathUtility.GetPathWithDirectorySeparator(projectPath);

            var projectsToRemove = slnFile.Projects.Where((p) =>
                    string.Equals(p.FilePath, projectPathNormalized, StringComparison.OrdinalIgnoreCase)).ToList();

            bool projectRemoved = false;
            if (projectsToRemove.Count == 0)
            {
                Reporter.Output.WriteLine(string.Format(
                    CommonLocalizableStrings.ProjectReferenceCouldNotBeFound,
                    projectPath));
            }
            else
            {
                foreach (var slnProject in projectsToRemove)
                {
                    var buildConfigsToRemove = slnFile.ProjectConfigurationsSection.GetPropertySet(slnProject.Id);
                    if (buildConfigsToRemove != null)
                    {
                        slnFile.ProjectConfigurationsSection.Remove(buildConfigsToRemove);
                    }

                    var nestedProjectsSection = slnFile.Sections.GetSection(
                        "NestedProjects",
                        SlnSectionType.PreProcess);
                    if (nestedProjectsSection != null && nestedProjectsSection.Properties.ContainsKey(slnProject.Id))
                    {
                        nestedProjectsSection.Properties.Remove(slnProject.Id);
                    }

                    slnFile.Projects.Remove(slnProject);
                    Reporter.Output.WriteLine(
                        string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, slnProject.FilePath));
                }

                foreach (var project in slnFile.Projects)
                {
                    var dependencies = project.Dependencies;
                    if (dependencies == null)
                    {
                        continue;
                    }

                    dependencies.SkipIfEmpty = true;

                    foreach (var removed in projectsToRemove)
                    {
                        dependencies.Properties.Remove(removed.Id);
                    }
                }

                projectRemoved = true;
            }

            return projectRemoved;
        }

        public static void RemoveEmptyConfigurationSections(this SlnFile slnFile)
        {
            if (slnFile.Projects.Count == 0)
            {
                var solutionConfigs = slnFile.Sections.GetSection("SolutionConfigurationPlatforms");
                if (solutionConfigs != null)
                {
                    slnFile.Sections.Remove(solutionConfigs);
                }

                var projectConfigs = slnFile.Sections.GetSection("ProjectConfigurationPlatforms");
                if (projectConfigs != null)
                {
                    slnFile.Sections.Remove(projectConfigs);
                }
            }
        }

        public static void RemoveEmptySolutionFolders(this SlnFile slnFile)
        {
            var solutionFolderProjects = slnFile.Projects
                .GetProjectsByType(ProjectTypeGuids.SolutionFolderGuid)
                .ToList();

            if (solutionFolderProjects.Any())
            {
                var nestedProjectsSection = slnFile.Sections.GetSection(
                    "NestedProjects",
                    SlnSectionType.PreProcess);

                if (nestedProjectsSection == null)
                {
                    foreach (var solutionFolderProject in solutionFolderProjects)
                    {
                        if (solutionFolderProject.Sections.Count() == 0)
                        {
                            slnFile.Projects.Remove(solutionFolderProject);
                        }
                    }
                }
                else
                {
                    var solutionFoldersInUse = slnFile.GetSolutionFoldersThatContainProjectsInItsHierarchy(
                        nestedProjectsSection.Properties);

                    foreach (var solutionFolderProject in solutionFolderProjects)
                    {
                        if (!solutionFoldersInUse.Contains(solutionFolderProject.Id))
                        {
                            nestedProjectsSection.Properties.Remove(solutionFolderProject.Id);
                            if (solutionFolderProject.Sections.Count() == 0)
                            {
                                slnFile.Projects.Remove(solutionFolderProject);
                            }
                        }
                    }

                    if (nestedProjectsSection.IsEmpty)
                    {
                        slnFile.Sections.Remove(nestedProjectsSection);
                    }
                }
            }
        }

        private static HashSet<string> GetSolutionFoldersThatContainProjectsInItsHierarchy(
            this SlnFile slnFile,
            SlnPropertySet nestedProjects)
        {
            var solutionFoldersInUse = new HashSet<string>();

            var nonSolutionFolderProjects = slnFile.Projects.GetProjectsNotOfType(
                ProjectTypeGuids.SolutionFolderGuid);

            foreach (var nonSolutionFolderProject in nonSolutionFolderProjects)
            {
                var id = nonSolutionFolderProject.Id;
                while (nestedProjects.ContainsKey(id))
                {
                    id = nestedProjects[id];
                    solutionFoldersInUse.Add(id);
                }
            }

            return solutionFoldersInUse;
        }
    }
}
