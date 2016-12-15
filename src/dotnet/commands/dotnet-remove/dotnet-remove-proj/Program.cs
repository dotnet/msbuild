// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Tools.Remove;

namespace Microsoft.DotNet.Tools.Remove.ProjectFromSolution
{
    public class RemoveProjectFromSolution : IRemoveSubCommand
    {
        private SlnFile _slnFile;

        public RemoveProjectFromSolution(string fileOrDirectory)
        {
            _slnFile = SlnFileFactory.CreateFromFileOrDirectory(fileOrDirectory);
        }

        public void Remove(IList<string> projectPaths)
        {
            if (projectPaths.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToRemove);
            }

            var relativeProjectPaths = projectPaths.Select((p) =>
                PathUtility.GetRelativePath(
                    PathUtility.EnsureTrailingSlash(_slnFile.BaseDirectory),
                    Path.GetFullPath(p))).ToList();

            bool slnChanged = false;
            foreach (var path in relativeProjectPaths)
            {
                slnChanged |= RemoveProject(_slnFile, path);
            }

            if (slnChanged)
            {
                _slnFile.Write();
            }
        }

        private static bool RemoveProject(SlnFile slnFile, string projectPath)
        {
            var projectPathNormalized = PathUtility.GetPathWithBackSlashes(projectPath);

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
                    slnFile.Projects.Remove(slnProject);
                    Reporter.Output.WriteLine(
                        string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, slnProject.FilePath));
                }

                projectRemoved = true;
            }

            return projectRemoved;
        }
    }

    public class RemoveProjectFromSolutionCommand : RemoveSubCommandBase
    {
        protected override string CommandName => "project";
        protected override string LocalizedDisplayName => LocalizableStrings.AppFullName;
        protected override string LocalizedDescription => LocalizableStrings.AppDescription;
        protected override string LocalizedHelpText => LocalizableStrings.AppHelpText;

        internal override void AddCustomOptions(CommandLineApplication app)
        {
        }

        protected override IRemoveSubCommand CreateIRemoveSubCommand(string fileOrDirectory)
        {
            return new RemoveProjectFromSolution(fileOrDirectory);
        }

        internal static CommandLineApplication CreateApplication(CommandLineApplication parentApp)
        {
            var removeSubCommand = new RemoveProjectFromSolutionCommand();
            return removeSubCommand.Create(parentApp);
        }
    }
}
