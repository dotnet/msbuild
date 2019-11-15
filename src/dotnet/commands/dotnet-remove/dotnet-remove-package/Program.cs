// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Tools.Remove.PackageReference
{
    internal class RemovePackageReferenceCommand : CommandBase
    {
        private readonly AppliedOption _appliedCommand;
        private readonly string _fileOrDirectory;

        public RemovePackageReferenceCommand(
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
            if (appliedCommand.Arguments.Count != 1)
            {
                throw new GracefulException(LocalizableStrings.SpecifyExactlyOnePackageReference);
            }

            _appliedCommand = appliedCommand;
            _fileOrDirectory = fileOrDirectory;
        }

        public override int Execute()
        {
            var projectFilePath = string.Empty;

            if (!File.Exists(_fileOrDirectory))
            {
                projectFilePath = MsbuildProject.GetProjectFileFromDirectory(_fileOrDirectory).FullName;
            }
            else
            {
                projectFilePath = _fileOrDirectory;
            }

            var packageToRemove = _appliedCommand.Arguments.Single();
            var result = NuGetCommand.Run(TransformArgs(packageToRemove, projectFilePath));

            return result;
        }

        private string[] TransformArgs(string packageId, string projectFilePath)
        {
            var args = new List<string>()
            {
                "package",
                "remove",
                "--package",
                packageId,
                "--project",
                projectFilePath
            };

            args.AddRange(_appliedCommand
                .OptionValuesToBeForwarded()
                .SelectMany(a => a.Split(' ')));

            return args.ToArray();
        }
    }
}
