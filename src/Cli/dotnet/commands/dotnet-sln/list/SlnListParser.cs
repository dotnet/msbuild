// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Sln;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.Sln.List;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class SlnListParser
    {
        public static readonly Option<bool> SolutionFolderOption = new Option<bool>(new string[] { "--solution-folders" }, LocalizableStrings.ListSolutionFoldersArgumentDescription);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("list", LocalizableStrings.ListAppFullName);

            command.AddOption(SolutionFolderOption);
            command.SetHandler((parseResult) => new ListProjectsInSolutionCommand(parseResult).Execute());

            return command;
        }
    }
}
