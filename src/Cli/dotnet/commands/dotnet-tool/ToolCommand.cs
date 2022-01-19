// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tool.List;
using Microsoft.DotNet.Tools.Tool.Restore;
using Microsoft.DotNet.Tools.Tool.Run;
using Microsoft.DotNet.Tools.Tool.Search;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using Microsoft.DotNet.Tools.Tool.Update;

namespace Microsoft.DotNet.Tools.Tool
{
    public class ToolCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "tool";
        protected override string FullCommandNameLocalized => LocalizableStrings.InstallFullCommandNameLocalized;
        protected override string ArgumentName => Constants.ProjectArgumentName;
        protected override string ArgumentDescriptionLocalized => CommonLocalizableStrings.ProjectArgumentDescription;

        internal override Dictionary<string, Func<ParseResult, CommandBase>> SubCommands =>
            new Dictionary<string, Func<ParseResult, CommandBase>>
            {
                ["install"] =
                appliedOption => new ToolInstallCommand(
                    ParseResult),
                ["uninstall"] =
                appliedOption => new ToolUninstallCommand(
                    ParseResult),
                ["update"] =
                appliedOption => new ToolUpdateCommand(
                    ParseResult),
                ["list"] =
                appliedOption => new ToolListCommand(
                    ParseResult),
                ["restore"] =
                appliedOption => new ToolRestoreCommand(
                    ParseResult),
                ["run"] =
                appliedOption => new ToolRunCommand(
                    ParseResult),
                ["search"] =
                appliedOption => new ToolSearchCommand(
                    ParseResult)
            };

        public static int Run(string[] args)
        {
            var command = new ToolCommand();
            return command.RunCommand(args);
        }
    }
}
