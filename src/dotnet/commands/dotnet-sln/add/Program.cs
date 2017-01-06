// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Sln;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Sln.Add
{
    internal class AddProjectToSolutionCommand : DotNetSubCommandBase
    {
        public static DotNetSubCommandBase Create()
        {
            var command = new AddProjectToSolutionCommand()
            {
                Name = "add",
                FullName = LocalizableStrings.AddAppFullName,
                Description = LocalizableStrings.AddSubcommandHelpText,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = LocalizableStrings.AddSubcommandHelpText,
            };

            command.HelpOption("-h|--help");

            return command;
        }

        public override int Run(string fileOrDirectory)
        {
            SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(fileOrDirectory);

            if (RemainingArguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd);
            }

            PathUtility.EnsureAllPathsExist(RemainingArguments, CommonLocalizableStrings.ProjectDoesNotExist);
            var fullProjectPaths = RemainingArguments.Select((p) => Path.GetFullPath(p)).ToList();

            int preAddProjectCount = slnFile.Projects.Count;
            foreach (var fullProjectPath in fullProjectPaths)
            {
                AddProject(slnFile, fullProjectPath);
            }

            if (slnFile.Projects.Count > preAddProjectCount)
            {
                slnFile.Write();
            }

            return 0;
        }

        private void AddProject(SlnFile slnFile, string fullProjectPath)
        {
            var relativeProjectPath = PathUtility.GetRelativePath(
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
                var projectInstance = new ProjectInstance(fullProjectPath);

                var slnProject = new SlnProject
                {
                    Id = projectInstance.GetProjectId(),
                    TypeGuid = projectInstance.GetProjectTypeGuid(),
                    Name = Path.GetFileNameWithoutExtension(relativeProjectPath),
                    FilePath = relativeProjectPath
                };

                AddDefaultBuildConfigurations(slnFile, slnProject);

                AddSolutionFolders(slnFile, slnProject);

                slnFile.Projects.Add(slnProject);

                Reporter.Output.WriteLine(
                    string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, relativeProjectPath));
            }
        }

        private void AddDefaultBuildConfigurations(SlnFile slnFile, SlnProject slnProject)
        {
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

        private void AddDefaultSolutionConfigurations(
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

        private void AddDefaultProjectConfigurations(
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

        private void AddSolutionFolders(SlnFile slnFile, SlnProject slnProject)
        {
            var solutionFolders = slnProject.GetSolutionFoldersFromProject();

            if (solutionFolders.Any())
            {
                var nestedProjectsSection = slnFile.Sections.GetOrCreateSection(
                    "NestedProjects",
                    SlnSectionType.PreProcess);

                string parentDirGuid = null;
                foreach (var dir in solutionFolders)
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

                nestedProjectsSection.Properties[slnProject.Id] = parentDirGuid;
            }
        }
    }
}
