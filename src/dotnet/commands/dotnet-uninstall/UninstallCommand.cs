// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Uninstall.Tool;

namespace Microsoft.DotNet.Tools.Uninstall
{
    public class UninstallCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "uninstall";
        protected override string FullCommandNameLocalized => LocalizableStrings.UninstallFullCommandName;
        protected override string ArgumentName => Constants.ToolPackageArgumentName;
        protected override string ArgumentDescriptionLocalized => LocalizableStrings.UninstallArgumentDescription;

        internal override Dictionary<string, Func<AppliedOption, CommandBase>> SubCommands =>
            new Dictionary<string, Func<AppliedOption, CommandBase>>
            {
                ["tool"] = options => new UninstallToolCommand(options["tool"], ParseResult)
            };

        public static int Run(string[] args)
        {
            return new UninstallCommand().RunCommand(args);
        }
    }
}
