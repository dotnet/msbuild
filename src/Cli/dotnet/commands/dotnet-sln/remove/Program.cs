// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Sln.Remove
{
    internal class RemoveProjectFromSolutionCommand : CommandBase
    {
        private readonly string _fileOrDirectory;
        private readonly IReadOnlyCollection<string> _arguments;

        public RemoveProjectFromSolutionCommand(ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValue(SlnCommandParser.SlnArgument);

            _arguments = (parseResult.GetValue(SlnRemoveParser.ProjectPathArgument) ?? Array.Empty<string>()).ToList().AsReadOnly();

            SlnArgumentValidator.ParseAndValidateArguments(_fileOrDirectory, _arguments, SlnArgumentValidator.CommandType.Remove);
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
