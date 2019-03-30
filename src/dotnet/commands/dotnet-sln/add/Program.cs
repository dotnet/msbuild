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

namespace Microsoft.DotNet.Tools.Sln.Add
{
    internal class AddProjectToSolutionCommand : CommandBase
    {
        private readonly AppliedOption _appliedCommand;
        private readonly string _fileOrDirectory;
        private readonly Func<SlnFile, string, IList<string>> _determineSolutionFolder;

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

            var inRoot = appliedCommand.ValueOrDefault<bool>(InRootOption);
            var relativeRoot = _appliedCommand.ValueOrDefault<string>(SolutionFolderOption);

            if (inRoot)
            {
                // The user requested all projects go to the root folder
                _determineSolutionFolder = (_, __) => null;
            }
            else if (!string.IsNullOrEmpty(relativeRoot))
            {
                // The user has specified an explicit root
                var solutionFolder = relativeRoot.Split(Path.DirectorySeparatorChar);
                _determineSolutionFolder = (_, __) => solutionFolder;
            }
            else
            {
                // We determine the root for each individual project
                _determineSolutionFolder = (slnFile, fullProjectPath) => {
                    var relativeProjectPath = Path.GetRelativePath(
                        PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory),
                        fullProjectPath);

                    return SlnProjectExtensions.GetSolutionFoldersFromProjectPath(relativeProjectPath);
                };
            }
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
                var solutionFolders = _determineSolutionFolder(slnFile, fullProjectPath);

                slnFile.AddProject(fullProjectPath, solutionFolders);
            }

            if (slnFile.Projects.Count > preAddProjectCount)
            {
                slnFile.Write();
            }

            return 0;
        }
    }
}
