// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System.Collections.Generic;
using Microsoft.DotNet.Tools.Remove;

namespace Microsoft.DotNet.Tools.Remove.ProjectToProjectReference
{
    public class RemoveProjectToProjectReference : IRemoveSubCommand
    {
        private CommandOption _frameworkOption;
        private MsbuildProject _msbuildProj;

        internal RemoveProjectToProjectReference(string fileOrDirectory, CommandOption frameworkOption)
        {
            _msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), fileOrDirectory);
            _frameworkOption = frameworkOption;
        }

        public void Remove(IList<string> references)
        {
            if (references.Count == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.SpecifyAtLeastOneReferenceToRemove);
            }

            int numberOfRemovedReferences = _msbuildProj.RemoveProjectToProjectReferences(
                _frameworkOption.Value(),
                references);

            if (numberOfRemovedReferences != 0)
            {
                _msbuildProj.ProjectRootElement.Save();
            }
        }
    }

    public class RemoveProjectToProjectReferenceCommand : RemoveSubCommandBase
    {
        private CommandOption _frameworkOption;
        
        protected override string CommandName => "p2p";
        protected override string LocalizedDisplayName => LocalizableStrings.AppFullName;
        protected override string LocalizedDescription => LocalizableStrings.AppDescription;
        protected override string LocalizedHelpText => LocalizableStrings.AppHelpText;

        internal override void AddCustomOptions(CommandLineApplication app)
        {
            _frameworkOption = app.Option(
                $"-f|--framework <{CommonLocalizableStrings.CmdFramework}>",
                LocalizableStrings.CmdFrameworkDescription,
                CommandOptionType.SingleValue);
        }

        protected override IRemoveSubCommand CreateIRemoveSubCommand(string fileOrDirectory)
        {
            return new RemoveProjectToProjectReference(fileOrDirectory, _frameworkOption);
        }

        internal static CommandLineApplication CreateApplication(CommandLineApplication parentApp)
        {
            var removeSubCommand = new RemoveProjectToProjectReferenceCommand();
            return removeSubCommand.Create(parentApp);
        }
    }
}
