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
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Tools.Add.PackageReference
{
    internal class AddPackageReferenceCommand : CommandBase
    {
        private readonly string _packageId;
        private readonly string _fileOrDirectory;

        public AddPackageReferenceCommand(
            ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = parseResult.GetValueForArgument(AddCommandParser.ProjectArgument);
            _packageId = parseResult.GetValueForArgument(AddPackageParser.CmdPackageArgument);
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

            var tempDgFilePath = string.Empty;

            if (!_parseResult.HasOption(AddPackageParser.NoRestoreOption))
            {
                
                try
                {
                    // Create a Dependency Graph file for the project
                    tempDgFilePath = Path.GetTempFileName();
                }
                catch (IOException ioex)
                {
                    // Catch IOException from Path.GetTempFileName() and throw a graceful exception to the user.
                    throw new GracefulException(string.Format(LocalizableStrings.CmdDGFileIOException, projectFilePath), ioex);
                }
                
                GetProjectDependencyGraph(projectFilePath, tempDgFilePath);
            }

            var result = NuGetCommand.Run(
                TransformArgs(
                    _packageId,
                    tempDgFilePath,
                    projectFilePath));
            DisposeTemporaryFile(tempDgFilePath);

            return result;
        }

        private void GetProjectDependencyGraph(string projectFilePath, string dgFilePath)
        {
            var args = new List<string>();

            // Pass the project file path
            args.Add(projectFilePath);

            // Pass the task as generate restore Dependency Graph file
            args.Add("-target:GenerateRestoreGraphFile");

            // Pass Dependency Graph file output path
            args.Add($"-property:RestoreGraphOutputPath=\"{dgFilePath}\"");

            // Turn off recursive restore
            args.Add($"-property:RestoreRecursive=false");

            // Turn off restore for Dotnet cli tool references so that we do not generate extra dg specs
            args.Add($"-property:RestoreDotnetCliToolReferences=false");

            // Output should not include MSBuild version header
            args.Add("-nologo");

            var result = new MSBuildForwardingApp(args).Execute();

            if (result != 0)
            {
                throw new GracefulException(string.Format(LocalizableStrings.CmdDGFileException, projectFilePath));
            }
        }

        private void DisposeTemporaryFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private string[] TransformArgs(string packageId, string tempDgFilePath, string projectFilePath)
        {
            var args = new List<string>
            {
                "package",
                "add",
                "--package",
                packageId,
                "--project",
                projectFilePath
            };

            args.AddRange(_parseResult
                .OptionValuesToBeForwarded(AddPackageParser.GetCommand())
                .SelectMany(a => a.Split(' ', 2)));

            if (_parseResult.HasOption(AddPackageParser.NoRestoreOption))
            {
                args.Add("--no-restore");
            }
            else
            {
                args.Add("--dg-file");
                args.Add(tempDgFilePath);
            }

            return args.ToArray();
        }
    }
}
