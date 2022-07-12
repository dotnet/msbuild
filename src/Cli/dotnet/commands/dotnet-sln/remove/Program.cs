// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Sln.Remove
{
    internal class RemoveProjectFromSolutionCommand : CommandBase
    {
        private readonly string _fileOrDirectory;
        private readonly IReadOnlyCollection<string> _arguments;

        public RemoveProjectFromSolutionCommand(
            ParseResult parseResult) : base(parseResult)
        {
            _arguments = (parseResult.GetValueForArgument(SlnRemoveParser.ProjectPathArgument) ?? Array.Empty<string>()).ToList().AsReadOnly();
            if (_arguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToRemove);
            }

            _fileOrDirectory = parseResult.GetValueForArgument(SlnCommandParser.SlnArgument);
        }

        public override int Execute()
        {
            SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(_fileOrDirectory);

            var baseDirectory = PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory);
            var relativeProjectPaths = _arguments.Select(p => {
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
