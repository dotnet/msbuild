// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Tools.Sln.Add
{
    internal class AddProjectToSolutionCommand : CommandBase
    {
        private readonly AppliedOption _appliedCommand;
        private readonly string _fileOrDirectory;
        private readonly bool _inRoot;
        private readonly IList<string> _relativeRootSolutionFolders;

        private const string InRootOption = "in-root";
        private const string SolutionFolderOption = "solution-folder";

        public AddProjectToSolutionCommand(
            AppliedOption appliedCommand,
            string fileOrDirectory,
            ParseResult parseResult) : base(parseResult)
        {
            if (appliedCommand == null)
            {
                throw new ArgumentNullException(nameof(appliedCommand));
            }
            _appliedCommand = appliedCommand;

            _fileOrDirectory = fileOrDirectory;

            _inRoot = appliedCommand.ValueOrDefault<bool>(InRootOption);
            string relativeRoot = _appliedCommand.ValueOrDefault<string>(SolutionFolderOption);

            if (_inRoot && !string.IsNullOrEmpty(relativeRoot))
            {
                // These two options are mutually exclusive
                throw new GracefulException(LocalizableStrings.SolutionFolderAndInRootMutuallyExclusive);
            }

            _relativeRootSolutionFolders = relativeRoot?.Split(Path.DirectorySeparatorChar);
        }

        public override int Execute()
        {
            SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(_fileOrDirectory);

            if (_appliedCommand.Arguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd);
            }

            PathUtility.EnsureAllPathsExist(_appliedCommand.Arguments, CommonLocalizableStrings.CouldNotFindProjectOrDirectory, true);

            var fullProjectPaths = _appliedCommand.Arguments.Select(p =>
            {
                var fullPath = Path.GetFullPath(p);
                return Directory.Exists(fullPath) ?
                    MsbuildProject.GetProjectFileFromDirectory(fullPath).FullName :
                    fullPath;
            }).ToList();

            var preAddProjectCount = slnFile.Projects.Count;

            foreach (var fullProjectPath in fullProjectPaths)
            {
                // Identify the intended solution folders
                var solutionFolders = DetermineSolutionFolder(slnFile, fullProjectPath);

                slnFile.AddProject(fullProjectPath, solutionFolders);
            }

            if (slnFile.Projects.Count > preAddProjectCount)
            {
                slnFile.Write();
            }

            return 0;
        }

        private static IList<string> GetSolutionFoldersFromProjectPath(string projectFilePath)
        {
            var solutionFolders = new List<string>();

            if (!IsPathInTreeRootedAtSolutionDirectory(projectFilePath))
                return solutionFolders;

            var currentDirString = $".{Path.DirectorySeparatorChar}";
            if (projectFilePath.StartsWith(currentDirString))
            {
                projectFilePath = projectFilePath.Substring(currentDirString.Length);
            }

            var projectDirectoryPath = TrimProject(projectFilePath);
            if (string.IsNullOrEmpty(projectDirectoryPath))
                return solutionFolders;

            var solutionFoldersPath = TrimProjectDirectory(projectDirectoryPath);
            if (string.IsNullOrEmpty(solutionFoldersPath))
                return solutionFolders;

            solutionFolders.AddRange(solutionFoldersPath.Split(Path.DirectorySeparatorChar));

            return solutionFolders;
        }

        private IList<string> DetermineSolutionFolder(SlnFile slnFile, string fullProjectPath)
        {
            if (_inRoot)
            {
                // The user requested all projects go to the root folder
                return null;
            }

            if (_relativeRootSolutionFolders != null)
            {
                // The user has specified an explicit root
                return _relativeRootSolutionFolders;
            }

            // We determine the root for each individual project
            var relativeProjectPath = Path.GetRelativePath(
                PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory),
                fullProjectPath);

            return GetSolutionFoldersFromProjectPath(relativeProjectPath);
        }

        private static bool IsPathInTreeRootedAtSolutionDirectory(string path)
        {
            return !path.StartsWith("..");
        }

        private static string TrimProject(string path)
        {
            return Path.GetDirectoryName(path);
        }

        private static string TrimProjectDirectory(string path)
        {
            return Path.GetDirectoryName(path);
        }
    }
}
