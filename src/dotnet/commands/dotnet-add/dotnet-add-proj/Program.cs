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

namespace Microsoft.DotNet.Tools.Add.ProjectToSolution
{
    public class AddProjectToSolutionCommand
    {
        internal static CommandLineApplication CreateApplication(CommandLineApplication parentApp)
        {
            CommandLineApplication app = parentApp.Command("project", throwOnUnexpectedArg: false);
            app.FullName = LocalizableStrings.AppFullName;
            app.Description = LocalizableStrings.AppDescription;
            app.HandleRemainingArguments = true;
            app.ArgumentSeparatorHelpText = LocalizableStrings.AppHelpText;

            app.HelpOption("-h|--help");

            app.OnExecute(() =>
            {
                try
                {
                    if (!parentApp.Arguments.Any())
                    {
                        throw new GracefulException(CommonLocalizableStrings.RequiredArgumentNotPassed, Constants.ProjectOrSolutionArgumentName);
                    }

                    var projectOrDirectory = parentApp.Arguments.First().Value;
                    if (string.IsNullOrEmpty(projectOrDirectory))
                    {
                        projectOrDirectory = PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory());
                    }

                    SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(projectOrDirectory);

                    if (app.RemainingArguments.Count == 0)
                    {
                        throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd);
                    }

                    List<string> projectPaths = app.RemainingArguments;
                    PathUtility.EnsureAllPathsExist(projectPaths, CommonLocalizableStrings.ProjectDoesNotExist);
                    var relativeProjectPaths = projectPaths.Select((p) =>
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
                catch (GracefulException e)
                {
                    Reporter.Error.WriteLine(e.Message.Red());
                    app.ShowHelp();
                    return 1;
                }
            });

            return app;
        }

        private static void AddProject(SlnFile slnFile, string projectPath)
        {
            var projectPathNormalized = PathUtility.GetPathWithBackSlashes(projectPath);

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

                slnFile.Projects.Add(slnProject);
                Reporter.Output.WriteLine(
                    string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectPath));
            }
        }
    }
}
