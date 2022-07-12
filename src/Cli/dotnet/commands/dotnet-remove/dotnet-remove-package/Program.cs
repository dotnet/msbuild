// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Tools.Remove.PackageReference
{
    internal class RemovePackageReferenceCommand : CommandBase
    {
        private readonly string _fileOrDirectory;
        private readonly IReadOnlyCollection<string> _arguments;

        public RemovePackageReferenceCommand(
            ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValueForArgument(RemoveCommandParser.ProjectArgument);
            _arguments = parseResult.GetValueForArgument(RemovePackageParser.CmdPackageArgument).ToList().AsReadOnly();
            if (_fileOrDirectory == null)
            {
                throw new ArgumentNullException(nameof(_fileOrDirectory));
            }
            if (_arguments.Count != 1)
            {
                throw new GracefulException(LocalizableStrings.SpecifyExactlyOnePackageReference);
            }
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

            var packageToRemove = _arguments.Single();
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

            args.AddRange(_parseResult
                .OptionValuesToBeForwarded(RemovePackageParser.GetCommand())
                .SelectMany(a => a.Split(' ')));

            return args.ToArray();
        }
    }
}
