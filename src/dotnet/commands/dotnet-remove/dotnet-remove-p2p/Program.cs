// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Remove.ProjectToProjectReference
{
    internal class RemoveProjectToProjectReferenceCommand : DotNetSubCommandBase
    {
        private CommandOption _frameworkOption;

        public static DotNetSubCommandBase Create()
        {
            var command = new RemoveProjectToProjectReferenceCommand()
            {
                Name = "p2p",
                FullName = LocalizableStrings.AppFullName,
                Description = LocalizableStrings.AppDescription,
                HandleRemainingArguments = true,
                ArgumentSeparatorHelpText = LocalizableStrings.AppHelpText,
            };

            command.HelpOption("-h|--help");

            command._frameworkOption = command.Option(
               $"-f|--framework <{CommonLocalizableStrings.CmdFramework}>",
               LocalizableStrings.CmdFrameworkDescription,
               CommandOptionType.SingleValue);

            return command;
        }

        public override int Run(string fileOrDirectory)
        {
            var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), fileOrDirectory);
            if (RemainingArguments.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneReferenceToRemove);
            }

            int numberOfRemovedReferences = msbuildProj.RemoveProjectToProjectReferences(
                _frameworkOption.Value(),
                RemainingArguments);

            if (numberOfRemovedReferences != 0)
            {
                msbuildProj.ProjectRootElement.Save();
            }

            return 0;
        }
    }
}
