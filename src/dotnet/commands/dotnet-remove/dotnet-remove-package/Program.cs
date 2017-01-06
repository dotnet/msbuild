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

namespace Microsoft.DotNet.Tools.Remove.PackageReference
{
    internal class RemovePackageReferenceCommand : DotNetSubCommandBase
    {

        public static DotNetSubCommandBase Create()
        {
            var command = new RemovePackageReferenceCommand
            {
                Name = "package",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = LocalizableStrings.AppHelpText,
            };

            command.HelpOption("-h|--help");

            return command;
        }

        public override int Run(string fileOrDirectory)
        {
            if (RemainingArguments.Count != 1)
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

            var packageToRemove = RemainingArguments.First();
            var result = NuGetCommand.Run(TransformArgs(packageToRemove, projectFilePath));

            return result;
        }

        private string[] TransformArgs(string packageId, string projectFilePath)
        {
            return new string[]{
                "package",
                "remove",
                "--package",
                packageId,
                "--project",
                projectFilePath
            };
        }
    }
}