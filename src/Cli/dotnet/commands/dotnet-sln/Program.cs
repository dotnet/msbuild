// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Sln.Add;
using Microsoft.DotNet.Tools.Sln.List;
using Microsoft.DotNet.Tools.Sln.Remove;

namespace Microsoft.DotNet.Tools.Sln
{
    public class SlnCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "sln";
        protected override string FullCommandNameLocalized => LocalizableStrings.AppFullName;
        protected override string ArgumentName => Constants.SolutionArgumentName;
        protected override string ArgumentDescriptionLocalized => LocalizableStrings.SolutionArgumentDescription;

        internal override Dictionary<string, Func<ParseResult, CommandBase>> SubCommands =>
            new Dictionary<string, Func<ParseResult, CommandBase>>
            {
                ["add"] =
                sln => new AddProjectToSolutionCommand(
                    ParseResult),

                ["list"] =
                sln => new ListProjectsInSolutionCommand(
                    ParseResult),

                ["remove"] =
                sln =>
                    new RemoveProjectFromSolutionCommand(
                        ParseResult)
            };

        public static int Run(string[] args)
        {
            var command = new SlnCommand();
            return command.RunCommand(args);
        }
    }
}
