// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tool.List;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using Microsoft.DotNet.Tools.Tool.Update;

namespace Microsoft.DotNet.Tools.Tool
{
    public class ToolCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "tool";
        protected override string FullCommandNameLocalized => LocalizableStrings.InstallFullCommandNameLocalized;
        protected override string ArgumentName => Constants.ProjectArgumentName;
        protected override string ArgumentDescriptionLocalized => CommonLocalizableStrings.ArgumentsProjectDescription;

        internal override Dictionary<string, Func<AppliedOption, CommandBase>> SubCommands =>
            new Dictionary<string, Func<AppliedOption, CommandBase>>
            {
                ["install"] =
                appliedOption => new ToolInstallCommand(
                    appliedOption["install"],
                    ParseResult),
                ["uninstall"] =
                appliedOption => new ToolUninstallCommand(
                    appliedOption["uninstall"],
                    ParseResult),
                ["update"] =
                appliedOption => new ToolUpdateCommand(
                    appliedOption["update"],
                    ParseResult),
                ["list"] =
                appliedOption => new ListToolCommand(
                    appliedOption["list"],
                    ParseResult)
            };

        public static int Run(string[] args)
        {
            var command = new ToolCommand();
            return command.RunCommand(args);
        }
    }
}
