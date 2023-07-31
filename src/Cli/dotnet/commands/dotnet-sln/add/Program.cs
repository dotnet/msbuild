// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Sln.Add
{
    internal class AddProjectToSolutionCommand : CommandBase
    {
        private readonly string _fileOrDirectory;
        private readonly bool _inRoot;
        private readonly IList<string> _relativeRootSolutionFolders;
        private readonly IReadOnlyCollection<string> _arguments;

        public AddProjectToSolutionCommand(ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);

            _arguments = parseResult.GetValue(SlnAddParser.ProjectPathArgument)?.ToArray() ?? (IReadOnlyCollection<string>)Array.Empty<string>();

            _inRoot = parseResult.GetValue(SlnAddParser.InRootOption);
            string relativeRoot = parseResult.GetValue(SlnAddParser.SolutionFolderOption);

            SlnArgumentValidator.ParseAndValidateArguments(_fileOrDirectory, _arguments, SlnArgumentValidator.CommandType.Add, _inRoot, relativeRoot);

            bool hasRelativeRoot = !string.IsNullOrEmpty(relativeRoot);

            if (hasRelativeRoot)
            {
                relativeRoot = PathUtility.GetPathWithDirectorySeparator(relativeRoot);
                _relativeRootSolutionFolders = relativeRoot.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                _relativeRootSolutionFolders = null;
            }
        }

        public override int Execute()
        {
            SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(_fileOrDirectory);

            var arguments = (_parseResult.GetValue<IEnumerable<string>>(SlnAddParser.ProjectPathArgument) ?? Array.Empty<string>()).ToList().AsReadOnly();
            if (arguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd);
            }

            PathUtility.EnsureAllPathsExist(arguments, CommonLocalizableStrings.CouldNotFindProjectOrDirectory, true);

            var fullProjectPaths = _arguments.Select(p =>
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
