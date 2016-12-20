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
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Add.PackageReference
{
    internal class AddPackageReferenceCommand : DotNetSubCommandBase
    {
        private CommandOption _frameworkOption;
        private CommandOption _noRestore;
        private CommandOption _source;
        private CommandOption _packageDirectory;

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

            command._frameworkOption = command.Option(
               $"-f|--framework",
               LocalizableStrings.CmdFrameworkDescription,
               CommandOptionType.SingleValue);

            command._noRestore = command.Option(
                $"-n|--no-restore ",
               LocalizableStrings.CmdNoRestoreDescription,
               CommandOptionType.NoValue);

            command._source = command.Option(
                $"-s|--source ",
                LocalizableStrings.CmdSourceDescription,
                CommandOptionType.SingleValue);

            command._packageDirectory = command.Option(
                $"--package-directory",
                LocalizableStrings.CmdPackageDirectoryDescription,
                CommandOptionType.SingleValue);

            return command;
        }

        public override int Run(string fileOrDirectory)
        {
            WaitForDebugger();
            var projects = new ProjectCollection();
            var msbuildProj = MsbuildProject.FromFileOrDirectory(projects, fileOrDirectory);

            if (RemainingArguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneReferenceToAdd);
            }

            var tempDgFilePath = CreateTemporaryFile(".dg");

            GetProjectDependencyGraph(msbuildProj.ProjectDirectory, tempDgFilePath);

            DisposeTemporaryFile(tempDgFilePath);

            return 0;
        }

        private void GetProjectDependencyGraph(string projectFilePath,
            string dgFilePath)
        {
            var args = new List<string>();

            // Pass the task as generate restore dg file
            args.Add("/t:GenerateRestoreGraphFile");

            // Pass dg file output path
            args.Add(string.Format("/p:RestoreGraphOutputPath={0}{1}{2}", '"', dgFilePath, '"'));

            var result = new MSBuildForwardingApp(args).Execute();

            if (result != 0)
            {
                throw new GracefulException("Could not generate dg file");
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

        public void WaitForDebugger()
        {
            Console.WriteLine("Waiting for debugger to attach.");
            Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");

            while (!Debugger.IsAttached)
            {
                System.Threading.Thread.Sleep(100);
            }
            Debugger.Break();
        }
    }
}