// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Add;
using Microsoft.DotNet.Tools.Install.Tool;
using LocalizableStrings = Microsoft.DotNet.Tools.Install.LocalizableStrings;

namespace Microsoft.DotNet.Tools.Install
{
    public class InstallCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "install";
        protected override string FullCommandNameLocalized => LocalizableStrings.InstallFullCommandNameLocalized;
        protected override string ArgumentName => Constants.ProjectArgumentName;
        protected override string ArgumentDescriptionLocalized => CommonLocalizableStrings.ArgumentsProjectDescription;

        internal override Dictionary<string, Func<AppliedOption, CommandBase>> SubCommands =>
            new Dictionary<string, Func<AppliedOption, CommandBase>>
            {
                ["tool"] =
                appliedOption => new InstallToolCommand(
                    appliedOption["tool"],
                    ParseResult)
            };

        public static int Run(string[] args)
        {
            var command = new InstallCommand();
            return command.RunCommand(args);
        }
    }
}
