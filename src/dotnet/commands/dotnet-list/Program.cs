// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.List.ProjectToProjectReferences;

namespace Microsoft.DotNet.Tools.List
{
    public class ListCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "list";
        protected override string FullCommandNameLocalized => LocalizableStrings.NetListCommand;
        internal override List<Func<CommandLineApplication, CommandLineApplication>> SubCommands =>
            new List<Func<CommandLineApplication, CommandLineApplication>>
            {
                ListProjectToProjectReferencesCommand.CreateApplication,
            };

        public static int Run(string[] args)
        {
            var command = new ListCommand();
            return command.RunCommand(args);
        }
    }
}
