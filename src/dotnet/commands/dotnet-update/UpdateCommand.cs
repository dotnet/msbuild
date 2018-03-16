// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Update
{
    public class UpdateCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "update";
        protected override string FullCommandNameLocalized => LocalizableStrings.UpdateFullCommandName;
        protected override string ArgumentName => Constants.ToolPackageArgumentName;
        protected override string ArgumentDescriptionLocalized => LocalizableStrings.UpdateArgumentDescription;

        internal override Dictionary<string, Func<AppliedOption, CommandBase>> SubCommands =>
            new Dictionary<string, Func<AppliedOption, CommandBase>>
            {
                ["tool"] = options => new Tool.UpdateToolCommand(options["tool"], ParseResult)
            };

        public static int Run(string[] args)
        {
            return new UpdateCommand().RunCommand(args);
        }
    }
}
