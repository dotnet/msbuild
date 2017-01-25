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

namespace Microsoft.DotNet.Tools.Sln.Remove
{
    internal class RemoveProjectFromSolutionCommand : DotNetSubCommandBase
    {
        public static DotNetSubCommandBase Create()
        {
            var command = new RemoveProjectFromSolutionCommand()
            {
                Name = "remove",
                FullName = LocalizableStrings.RemoveAppFullName,
                Description = LocalizableStrings.RemoveSubcommandHelpText,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = LocalizableStrings.RemoveSubcommandHelpText,
            };

            command.HelpOption("-h|--help");

            return command;
        }

        public override int Run(string fileOrDirectory)
        {
            SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(fileOrDirectory);

            if (RemainingArguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneProjectToRemove);
            }

            var relativeProjectPaths = RemainingArguments.Select((p) =>
                PathUtility.GetRelativePath(
                    PathUtility.EnsureTrailingSlash(slnFile.BaseDirectory),
                    Path.GetFullPath(p))).ToList();

            bool slnChanged = false;
            foreach (var path in relativeProjectPaths)
            {
                slnChanged |= slnFile.RemoveProject(path);
            }

            slnFile.RemoveEmptyConfigurationSections();

            slnFile.RemoveEmptySolutionFolders();

            if (slnChanged)
            {
                slnFile.Write();
            }

            return 0;
        }
    }
}
