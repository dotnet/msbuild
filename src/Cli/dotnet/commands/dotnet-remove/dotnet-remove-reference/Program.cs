// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Remove.ProjectToProjectReference
{
    internal class RemoveProjectToProjectReferenceCommand : CommandBase
    {
        private readonly string _fileOrDirectory;
        private readonly IReadOnlyCollection<string> _arguments;

        public RemoveProjectToProjectReferenceCommand(
            ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValueForArgument(RemoveCommandParser.ProjectArgument);
            _arguments = parseResult.GetValueForArgument(RemoveProjectToProjectReferenceParser.ProjectPathArgument).ToList().AsReadOnly();

            if (_arguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneReferenceToRemove);
            }
        }

        public override int Execute()
        {
            var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), _fileOrDirectory, false);
            var references = _arguments.Select(p => {
                var fullPath = Path.GetFullPath(p);
                if (!Directory.Exists(fullPath))
                {
                    return p;
                }

                return Path.GetRelativePath(
                    msbuildProj.ProjectRootElement.FullPath,
                    MsbuildProject.GetProjectFileFromDirectory(fullPath).FullName
                );
            });

            int numberOfRemovedReferences = msbuildProj.RemoveProjectToProjectReferences(
                _parseResult.GetValueForOption(RemoveProjectToProjectReferenceParser.FrameworkOption),
                references);

            if (numberOfRemovedReferences != 0)
            {
                msbuildProj.ProjectRootElement.Save();
            }

            return 0;
        }
    }
}
