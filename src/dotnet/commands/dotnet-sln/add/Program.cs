// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Sln;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Sln.Add
{
    internal class AddProjectToSolutionCommand : DotNetSubCommandBase
    {
        public static DotNetSubCommandBase Create()
        {
            var command = new AddProjectToSolutionCommand()
            {
                Name = "add",
                FullName = LocalizableStrings.AddAppFullName,
                Description = LocalizableStrings.AddSubcommandHelpText,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = LocalizableStrings.AddSubcommandHelpText,
            };

            command.HelpOption("-h|--help");

            return command;
        }

        public override int Run(string fileOrDirectory)
        {
            SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(fileOrDirectory);

            if (RemainingArguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd);
            }

            PathUtility.EnsureAllPathsExist(RemainingArguments, CommonLocalizableStrings.ProjectDoesNotExist);
            var fullProjectPaths = RemainingArguments.Select((p) => Path.GetFullPath(p)).ToList();

            int preAddProjectCount = slnFile.Projects.Count;
            foreach (var fullProjectPath in fullProjectPaths)
            {
                slnFile.AddProject(fullProjectPath);
            }

            if (slnFile.Projects.Count > preAddProjectCount)
            {
                slnFile.Write();
            }

            return 0;
        }
    }
}
