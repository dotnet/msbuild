// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Add.PackageReference;
using Microsoft.DotNet.Tools.Add.ProjectToProjectReference;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.DotNet.Tools.Sln.Add;

namespace Microsoft.DotNet.Tools.New
{
    internal static class DotnetCommandCallbacks
    {
        internal static bool AddPackageReference(string projectPath, string packageName, string? version)
        {
            PathUtility.EnsureAllPathsExist(new[] { projectPath }, CommonLocalizableStrings.FileNotFound, allowDirectories: false);
            IEnumerable<string> commandArgs = new[] { "add", projectPath, "package", packageName };
            if (!string.IsNullOrWhiteSpace(version))
            {
                commandArgs = commandArgs.Append(AddPackageParser.VersionOption.Name).Append(version);
            }
            var addPackageReferenceCommand = new AddPackageReferenceCommand(AddCommandParser.GetCommand().Parse(commandArgs.ToArray()));
            return addPackageReferenceCommand.Execute() == 0;
        }

        internal static bool AddProjectReference(string projectPath, string projectToAdd)
        {
            PathUtility.EnsureAllPathsExist(new[] { projectPath }, CommonLocalizableStrings.FileNotFound, allowDirectories: false);
            PathUtility.EnsureAllPathsExist(new[] { projectToAdd }, CommonLocalizableStrings.FileNotFound, allowDirectories: false);
            IEnumerable<string> commandArgs = new[] { "add", projectPath, "reference", projectToAdd };
            var addProjectReferenceCommand = new AddProjectToProjectReferenceCommand(AddCommandParser.GetCommand().Parse(commandArgs.ToArray()));
            return addProjectReferenceCommand.Execute() == 0;
        }

        internal static bool RestoreProject(string pathToRestore)
        {
            PathUtility.EnsureAllPathsExist(new[] { pathToRestore }, CommonLocalizableStrings.FileNotFound, allowDirectories: true);
            return RestoreCommand.Run(new string[] { pathToRestore }) == 0;
        }

        internal static bool AddProjectsToSolution(string solutionPath, IReadOnlyList<string> projectsToAdd, string? solutionFolder, bool? inRoot)
        {
            PathUtility.EnsureAllPathsExist(new[] { solutionPath }, CommonLocalizableStrings.FileNotFound, allowDirectories: false);
            PathUtility.EnsureAllPathsExist(projectsToAdd, CommonLocalizableStrings.FileNotFound, allowDirectories: false);
            IEnumerable<string> commandArgs = new[] { "sln", solutionPath, "add" }.Concat(projectsToAdd);
            if (!string.IsNullOrWhiteSpace(solutionFolder))
            {
                commandArgs = commandArgs.Append(SlnAddParser.SolutionFolderOption.Name).Append(solutionFolder);
            }

            if (inRoot is true)
            {
                commandArgs = commandArgs.Append(SlnAddParser.InRootOption.Name);
            }
            var addProjectToSolutionCommand = new AddProjectToSolutionCommand(SlnCommandParser.GetCommand().Parse(commandArgs.ToArray()));
            return addProjectToSolutionCommand.Execute() == 0;
        }
    }
}
