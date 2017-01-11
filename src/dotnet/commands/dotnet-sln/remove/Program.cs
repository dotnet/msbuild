// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Sln;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Sln.Remove
{
    internal class RemoveProjectFromSolutionCommand : DotNetSubCommandBase
    {
        public static DotNetSubCommandBase Create()
        {
            var command = new RemoveProjectFromSolutionCommand()
            {
                Name = "remove",
                FullName = LocalizableStrings.RemoveAppFullName,
                Description = LocalizableStrings.RemoveSubcommandHelpText,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = LocalizableStrings.RemoveSubcommandHelpText,
            };

            command.HelpOption("-h|--help");

            return command;
        }

        public override int Run(string fileOrDirectory)
        {
            SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(fileOrDirectory);

            if (RemainingArguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToRemove);
            }

            var relativeProjectPaths = RemainingArguments.Select((p) =>
                PathUtility.GetRelativePath(
                    PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory),
                    Path.GetFullPath(p))).ToList();

            bool slnChanged = false;
            foreach (var path in relativeProjectPaths)
            {
                slnChanged |= RemoveProject(slnFile, path);
            }

            RemoveEmptyConfigurationSections(slnFile);

            RemoveEmptySolutionFolders(slnFile);

            if (slnChanged)
            {
                slnFile.Write();
            }

            return 0;
        }

        private bool RemoveProject(SlnFile slnFile, string projectPath)
        {
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

                projectRemoved = true;
            }

            return projectRemoved;
        }

        private void RemoveEmptyConfigurationSections(SlnFile slnFile)
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

        private void RemoveEmptySolutionFolders(SlnFile slnFile)
        {
            var referencedSolutionFolders = slnFile.Projects.GetReferencedSolutionFolders();

            var solutionFolderProjects = slnFile.Projects
                .Where(p => p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid)
                .ToList();

            if (solutionFolderProjects.Any())
            {
                var nestedProjectsSection = slnFile.Sections.GetSection(
                    "NestedProjects",
                    SlnSectionType.PreProcess);

                foreach (var solutionFolderProject in solutionFolderProjects)
                {
                    if (!referencedSolutionFolders.Contains(solutionFolderProject.Name))
                    {
                        slnFile.Projects.Remove(solutionFolderProject);
                        nestedProjectsSection.Properties.Remove(solutionFolderProject.Id);
                    }
                }

                if (nestedProjectsSection.IsEmpty)
                {
                    slnFile.Sections.Remove(nestedProjectsSection);
                }
            }
        }
    }
}
