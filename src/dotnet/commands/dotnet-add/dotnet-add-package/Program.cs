// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.NuGet;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Tools.Add.PackageReference
{
    internal class AddPackageReferenceCommand : DotNetSubCommandBase
    {
        private CommandOption _versionOption;
        private CommandOption _frameworkOption;
        private CommandOption _noRestoreOption;
        private CommandOption _sourceOption;
        private CommandOption _packageDirectoryOption;
        private CommandArgument _packageNameArgument;

        public static DotNetSubCommandBase Create()
        {
            var command = new AddPackageReferenceCommand
            {
                Name = "package",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription,
                HandleRemainingArguments = false
            };

            command.HelpOption("-h|--help");

            command._packageNameArgument = command.Argument(
                $"<{LocalizableStrings.CmdPackage}>",
                LocalizableStrings.CmdPackageDescription,
                multipleValues: false);

            command._versionOption = command.Option(
                $"-v|--version <{LocalizableStrings.CmdVersion}>",
                LocalizableStrings.CmdVersionDescription,
                CommandOptionType.SingleValue);

            command._frameworkOption = command.Option(
               $"-f|--framework <{LocalizableStrings.CmdFramework}>",
               LocalizableStrings.CmdFrameworkDescription,
               CommandOptionType.SingleValue);

            command._noRestoreOption = command.Option(
                "-n|--no-restore ",
               LocalizableStrings.CmdNoRestoreDescription,
               CommandOptionType.NoValue);

            command._sourceOption = command.Option(
                $"-s|--source <{LocalizableStrings.CmdSource}>",
                LocalizableStrings.CmdSourceDescription,
                CommandOptionType.SingleValue);

            command._packageDirectoryOption = command.Option(
                $"--package-directory <{LocalizableStrings.CmdPackageDirectory}>",
                LocalizableStrings.CmdPackageDirectoryDescription,
                CommandOptionType.SingleValue);

            return command;
        }

        public override int Run(string fileOrDirectory)
        {
            if (_packageNameArgument.Values.Count != 1 || string.IsNullOrWhiteSpace(_packageNameArgument.Value) || RemainingArguments.Count > 0)
            {
                throw new GracefulException(LocalizableStrings.SpecifyExactlyOnePackageReference);
            }

            var projectFilePath = string.Empty;

            if (!File.Exists(fileOrDirectory))
            {
                projectFilePath = MsbuildProject.GetProjectFileFromDirectory(fileOrDirectory).FullName;
            }
            else
            {
                projectFilePath = fileOrDirectory;
            }

            var tempDgFilePath = string.Empty;

            if (!_noRestoreOption.HasValue())
            {
                // Create a Dependency Graph file for the project
                tempDgFilePath = Path.GetTempFileName();
                GetProjectDependencyGraph(projectFilePath, tempDgFilePath);
            }

            var result = NuGetCommand.Run(TransformArgs(_packageNameArgument.Value, tempDgFilePath, projectFilePath));
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
            var args = new List<string>(){
                "package",
                "add",
                "--package",
                packageId,
                "--project",
                projectFilePath
            };

            if (_versionOption.HasValue())
            {
                args.Add("--version");
                args.Add(_versionOption.Value());
            }
            if (_sourceOption.HasValue())
            {
                args.Add("--source");
                args.Add(_sourceOption.Value());
            }
            if (_frameworkOption.HasValue())
            {
                args.Add("--framework");
                args.Add(_frameworkOption.Value());
            }
            if (_packageDirectoryOption.HasValue())
            {
                args.Add("--package-directory");
                args.Add(_packageDirectoryOption.Value());
            }
            if (_noRestoreOption.HasValue())
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