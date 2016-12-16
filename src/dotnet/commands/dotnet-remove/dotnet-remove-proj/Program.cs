// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Remove.ProjectFromSolution
{
    internal class RemoveProjectFromSolutionCommand : DotNetSubCommandBase
    {
        public static DotNetSubCommandBase Create()
        {
            var command = new RemoveProjectFromSolutionCommand()
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

            if (slnChanged)
            {
                slnFile.Write();
            }

            return 0;
        }

        private bool RemoveProject(SlnFile slnFile, string projectPath)
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
}
