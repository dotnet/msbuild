// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Sln.Remove
{
    internal class RemoveProjectFromSolutionCommand : CommandBase
    {
        private readonly AppliedOption _appliedCommand;
        private readonly string _fileOrDirectory;

        public RemoveProjectFromSolutionCommand(
            AppliedOption appliedCommand, 
            string fileOrDirectory,
            ParseResult parseResult) : base(parseResult)
        {
            if (appliedCommand == null)
            {
                throw new ArgumentNullException(nameof(appliedCommand));
            }

            if (appliedCommand.Arguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToRemove);
            }

            _appliedCommand = appliedCommand;
            _fileOrDirectory = fileOrDirectory;
        }

        public override int Execute()
        {
            SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(_fileOrDirectory);

            var baseDirectory = PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory);
            var relativeProjectPaths = _appliedCommand.Arguments.Select(p => {
                var fullPath = Path.GetFullPath(p);
                return Path.GetRelativePath(
                    baseDirectory,
                    Directory.Exists(fullPath) ?
                        MsbuildProject.GetProjectFileFromDirectory(fullPath).FullName :
                        fullPath
                );
            });

            bool slnChanged = false;
            foreach (var path in relativeProjectPaths)
            {
                slnChanged |= slnFile.RemoveProject(path);
            }

            slnFile.RemoveEmptyConfigurationSections();

            slnFile.RemoveEmptySolutionFolders();

            if (slnChanged)
            {
                slnFile.Write();
            }

            return 0;
        }
    }
}