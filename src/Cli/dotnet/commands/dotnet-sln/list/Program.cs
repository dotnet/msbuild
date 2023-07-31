// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Sln.List
{
    internal class ListProjectsInSolutionCommand : CommandBase
    {
        private readonly string _fileOrDirectory;
        private readonly bool _displaySolutionFolders;

        public ListProjectsInSolutionCommand(
            ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);
            _displaySolutionFolders = parseResult.GetValue(SlnListParser.SolutionFolderOption);
        }

        public override int Execute()
        {
            var slnFile = SlnFileFactory.CreateFromFileOrDirectory(_fileOrDirectory);

            string[] paths;

            if (_displaySolutionFolders)
            {
                paths = slnFile.Projects
                    .GetProjectsByType(ProjectTypeGuids.SolutionFolderGuid)
                    .Select(folder => folder.GetFullSolutionFolderPath())
                    .ToArray();
            }
            else
            {
                paths = slnFile.Projects
                    .GetProjectsNotOfType(ProjectTypeGuids.SolutionFolderGuid)
                    .Select(project => project.FilePath)
                    .ToArray();
            }

            if (paths.Length == 0)
            {
                Reporter.Output.WriteLine(CommonLocalizableStrings.NoProjectsFound);
            }
            else
            {
                Array.Sort(paths);

                string header = _displaySolutionFolders ? LocalizableStrings.SolutionFolderHeader : LocalizableStrings.ProjectsHeader;
                Reporter.Output.WriteLine($"{header}");
                Reporter.Output.WriteLine(new string('-', header.Length));
                foreach (string slnProject in paths)
                {
                    Reporter.Output.WriteLine(slnProject);
                }
            }
            return 0;
        }
    }
}
