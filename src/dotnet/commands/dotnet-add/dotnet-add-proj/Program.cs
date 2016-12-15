// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Tools.Add;

namespace Microsoft.DotNet.Tools.Add.ProjectToSolution
{
    public class AddProjectToSolution : IAddSubCommand
    {
        private SlnFile _slnFile;

        public AddProjectToSolution(string fileOrDirectory)
        {
            _slnFile = SlnFileFactory.CreateFromFileOrDirectory(fileOrDirectory);
        }

        public int Add(List<string> projectPaths)
        {
            if (projectPaths.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd);
            }

            PathUtility.EnsureAllPathsExist(projectPaths, CommonLocalizableStrings.ProjectDoesNotExist);
            var relativeProjectPaths = projectPaths.Select((p) =>
                PathUtility.GetRelativePath(
                    PathUtility.EnsureTrailingSlash(_slnFile.BaseDirectory),
                    Path.GetFullPath(p))).ToList();

            int preAddProjectCount = _slnFile.Projects.Count;
            foreach (var project in relativeProjectPaths)
            {
                AddProject(project);
            }

            if (_slnFile.Projects.Count > preAddProjectCount)
            {
                _slnFile.Write();
            }

            return 0;
        }

        private void AddProject(string projectPath)
        {
            var projectPathNormalized = PathUtility.GetPathWithBackSlashes(projectPath);

            if (_slnFile.Projects.Any((p) =>
                    string.Equals(p.FilePath, projectPathNormalized, StringComparison.OrdinalIgnoreCase)))
            {
                Reporter.Output.WriteLine(string.Format(
                    CommonLocalizableStrings.SolutionAlreadyContainsProject,
                    _slnFile.FullPath,
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

                _slnFile.Projects.Add(slnProject);
                Reporter.Output.WriteLine(
                    string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectPath));
            }
        }
    }

    public class AddProjectToSolutionCommand : AddSubCommandBase
    {
        protected override string CommandName => "project";
        protected override string LocalizedDisplayName => LocalizableStrings.AppFullName;
        protected override string LocalizedDescription => LocalizableStrings.AppDescription;
        protected override string LocalizedHelpText => LocalizableStrings.AppHelpText;

        internal override void AddCustomOptions(CommandLineApplication app)
        {
        }

        protected override IAddSubCommand CreateIAddSubCommand(string fileOrDirectory)
        {
            return new AddProjectToSolution(fileOrDirectory);
        }

        internal static CommandLineApplication CreateApplication(CommandLineApplication parentApp)
        {
            var addSubCommand = new AddProjectToSolutionCommand();
            return addSubCommand.Create(parentApp);
        }
    }
}
