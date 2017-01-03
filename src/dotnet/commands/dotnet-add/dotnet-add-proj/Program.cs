// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Add.ProjectToSolution
{
    internal class AddProjectToSolutionCommand : DotNetSubCommandBase
    {
        public static DotNetSubCommandBase Create()
        {
            var command = new AddProjectToSolutionCommand()
            {
                Name = "project",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = LocalizableStrings.AppHelpText,
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
            var relativeProjectPaths = RemainingArguments.Select((p) =>
                PathUtility.GetRelativePath(
                    PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory),
                    Path.GetFullPath(p))).ToList();

            int preAddProjectCount = slnFile.Projects.Count;
            foreach (var project in relativeProjectPaths)
            {
                AddProject(slnFile, project);
            }

            if (slnFile.Projects.Count > preAddProjectCount)
            {
                slnFile.Write();
            }

            return 0;
        }

        private void AddProject(SlnFile slnFile, string projectPath)
        {
            var projectPathNormalized = PathUtility.GetPathWithDirectorySeparator(projectPath);

            if (slnFile.Projects.Any((p) =>
                    string.Equals(p.FilePath, projectPathNormalized, StringComparison.OrdinalIgnoreCase)))
            {
                Reporter.Output.WriteLine(string.Format(
                    CommonLocalizableStrings.SolutionAlreadyContainsProject,
                    slnFile.FullPath,
                    projectPath));
            }
            else
            {
                string projectGuidString = null;
                if (File.Exists(projectPath))
                {
                    var projectElement = ProjectRootElement.Open(
                        projectPath,
                        new ProjectCollection(),
                        preserveFormatting: true);

                    var projectGuidProperty = projectElement.Properties.Where((p) =>
                        string.Equals(p.Name, "ProjectGuid", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                    if (projectGuidProperty != null)
                    {
                        projectGuidString = projectGuidProperty.Value;
                    }
                }

                var projectGuid = (projectGuidString == null)
                    ? Guid.NewGuid()
                    : new Guid(projectGuidString);

                var slnProject = new SlnProject
                {
                    Id = projectGuid.ToString("B").ToUpper(),
                    TypeGuid = ProjectTypeGuids.CPSProjectTypeGuid,
                    Name = Path.GetFileNameWithoutExtension(projectPath),
                    FilePath = projectPathNormalized
                };

                AddDefaultBuildConfigurations(slnFile, slnProject);

                slnFile.Projects.Add(slnProject);

                Reporter.Output.WriteLine(
                    string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectPath));
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
    }
}
