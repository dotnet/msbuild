// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Tools.Add.PackageReference
{
    internal class AddPackageReferenceCommand : CommandBase
    {
        private readonly AppliedOption _appliedCommand;

        private readonly string _packageId;
        private readonly string _fileOrDirectory;

        public AddPackageReferenceCommand(AppliedOption appliedCommand, string fileOrDirectory)
        {
            _appliedCommand = appliedCommand;
            _fileOrDirectory = fileOrDirectory;
            _packageId = appliedCommand.Value<string>();

            
            if ( string.IsNullOrWhiteSpace(_packageId) || _appliedCommand.Arguments.Count > 1) 
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

            var tempDgFilePath = string.Empty;

            if (!_appliedCommand.HasOption("no-restore"))
            {
                // Create a Dependency Graph file for the project
                tempDgFilePath = Path.GetTempFileName();
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
            args.Add("/t:GenerateRestoreGraphFile");

            // Pass Dependency Graph file output path
            args.Add($"/p:RestoreGraphOutputPath=\"{dgFilePath}\"");

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

            args.AddRange(_appliedCommand
                .OptionValuesToBeForwarded()
                .SelectMany(a => a.Split(' ')));

            if (_appliedCommand.HasOption("no-restore"))
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