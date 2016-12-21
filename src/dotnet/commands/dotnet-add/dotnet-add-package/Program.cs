// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.NuGet;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Add.PackageReference
{
    internal class AddPackageReferenceCommand : DotNetSubCommandBase
    {
        private CommandOption _versionOption;
        private CommandOption _frameworkOption;
        private CommandOption _noRestoreOption;
        private CommandOption _sourceOption;
        private CommandOption _packageDirectoryOption;

        public static DotNetSubCommandBase Create()
        {
            var command = new AddPackageReferenceCommand()
            {
                Name = "package",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = LocalizableStrings.AppHelpText,
            };

            command.HelpOption("-h|--help");

            command._versionOption = command.Option(
                $"-v|--version",
                LocalizableStrings.CmdVersionDescription,
                CommandOptionType.SingleValue);

            command._frameworkOption = command.Option(
               $"-f|--framework",
               LocalizableStrings.CmdFrameworkDescription,
               CommandOptionType.SingleValue);

            command._noRestoreOption = command.Option(
                $"-n|--no-restore ",
               LocalizableStrings.CmdNoRestoreDescription,
               CommandOptionType.NoValue);

            command._sourceOption = command.Option(
                $"-s|--source ",
                LocalizableStrings.CmdSourceDescription,
                CommandOptionType.SingleValue);

            command._packageDirectoryOption = command.Option(
                $"--package-directory",
                LocalizableStrings.CmdPackageDirectoryDescription,
                CommandOptionType.SingleValue);

            return command;
        }

        public override int Run(string fileOrDirectory)
        {
            var projects = new ProjectCollection();
            var msbuildProj = MsbuildProject.FromFileOrDirectory(projects, fileOrDirectory);

            var x = this.Arguments;
            if (RemainingArguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneReferenceToAdd);
            }

            var tempDgFilePath = CreateTemporaryFile(".dg");

            GetProjectDependencyGraph(msbuildProj.ProjectRootElement.FullPath, tempDgFilePath);
            
            var result = NuGetCommand.Run(TransformArgs(tempDgFilePath, msbuildProj.ProjectRootElement.FullPath));
            DisposeTemporaryFile(tempDgFilePath);
            return 0;
        }

        private void GetProjectDependencyGraph(string projectFilePath,
            string dgFilePath)
        {
            var args = new List<string>();

            // Pass the project file path
            args.Add(projectFilePath);

            // Pass the task as generate restore dg file
            args.Add("/t:GenerateRestoreGraphFile");

            // Pass dg file output path
            args.Add(string.Format("/p:RestoreGraphOutputPath={0}{1}{2}", '"', dgFilePath, '"'));

            var result = new MSBuildForwardingApp(args).Execute();

            if (result != 0)
            {
                throw new GracefulException(string.Format(LocalizableStrings.CmdDGFileException, projectFilePath));
            }
        }

        private string CreateTemporaryFile(string extension)
        {
            var tempDirectory = Path.GetTempPath();
            var tempFile = Path.Combine(tempDirectory, Guid.NewGuid().ToString() + extension);
            File.Create(tempFile).Dispose();
            return tempFile;
        }

        private void DisposeTemporaryFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private string[] TransformArgs(string tempDgFilePath, string projectFilePath)
        {
            var args = new List<string>(){
                "package",
                "add",
                "--package",
                "Newtonsoft.Json",
                "--project",
                projectFilePath,
                "--dg-file",
                tempDgFilePath
            };
            if(_versionOption.HasValue())
            {
                args.Append("--version");
                args.Append(_versionOption.Value());
            }
            if(_sourceOption.HasValue())
            {
                args.Append("--source");
                args.Append(_sourceOption.Value());
            }
            if(_frameworkOption.HasValue())
            {
                args.Append("--framework");
                args.Append(_frameworkOption.Value());
            }
            if(_noRestoreOption.HasValue())
            {
                args.Append("--no-restore");
            }
            if(_packageDirectoryOption.HasValue())
            {
                args.Append("--package-directory");
                args.Append(_packageDirectoryOption.Value());
            }

            return args.ToArray();
        }
    }
}