// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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
            _fileOrDirectory = parseResult.GetValue(RemoveCommandParser.ProjectArgument);
            _arguments = parseResult.GetValue(RemovePackageParser.CmdPackageArgument).ToList().AsReadOnly();
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
