// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Remove.ProjectToProjectReference
{
    internal class RemoveProjectToProjectReferenceCommand : CommandBase
    {
        private readonly AppliedOption _appliedCommand;
        private readonly string _fileOrDirectory;

        public RemoveProjectToProjectReferenceCommand(
            AppliedOption appliedCommand,
            string fileOrDirectory,
            ParseResult parseResult) : base(parseResult)
        {
            if (appliedCommand == null)
            {
                throw new ArgumentNullException(nameof(appliedCommand));
            }

            if (fileOrDirectory == null)
            {
                throw new ArgumentNullException(nameof(fileOrDirectory));
            }

            if (appliedCommand.Arguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneReferenceToRemove);
            }

            _appliedCommand = appliedCommand;
            _fileOrDirectory = fileOrDirectory;
        }

        public override int Execute()
        {
            var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), _fileOrDirectory, false);
            var references = _appliedCommand.Arguments.Select(p => {
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
                _appliedCommand.ValueOrDefault<string>("framework"),
                references);

            if (numberOfRemovedReferences != 0)
            {
                msbuildProj.ProjectRootElement.Save();
            }

            return 0;
        }
    }
}
