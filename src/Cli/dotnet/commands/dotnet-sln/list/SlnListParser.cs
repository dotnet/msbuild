// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
