// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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
            _fileOrDirectory = parseResult.GetValue(RemoveCommandParser.ProjectArgument);
            _arguments = parseResult.GetValue(RemoveProjectToProjectReferenceParser.ProjectPathArgument).ToList().AsReadOnly();

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
                _parseResult.GetValue(RemoveProjectToProjectReferenceParser.FrameworkOption),
                references);

            if (numberOfRemovedReferences != 0)
            {
                msbuildProj.ProjectRootElement.Save();
            }

            return 0;
        }
    }
}
