// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Sln.List
{
    internal class ListProjectsInSolutionCommand : CommandBase
    {
        private readonly string _fileOrDirectory;

        public ListProjectsInSolutionCommand(
            AppliedOption appliedCommand, 
            string fileOrDirectory,
            ParseResult parseResult) : base(parseResult)
        {
            if (appliedCommand == null)
            {
                throw new ArgumentNullException(nameof(appliedCommand));
            }
            _fileOrDirectory = fileOrDirectory;
        }

        public override int Execute()
        {
            SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(_fileOrDirectory);
            if (slnFile.Projects.Count == 0)
            {
                Reporter.Output.WriteLine(CommonLocalizableStrings.NoProjectsFound);
            }
            else
            {
                Reporter.Output.WriteLine($"{LocalizableStrings.ProjectsHeader}");
                Reporter.Output.WriteLine(new string('-', LocalizableStrings.ProjectsHeader.Length));
                foreach (var slnProject in slnFile.Projects.Where(p => p.TypeGuid != ProjectTypeGuids.SolutionFolderGuid))
                {
                    Reporter.Output.WriteLine(slnProject.FilePath);
                }
            }
            return 0;
        }
    }
}