// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Search;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolSearchCommandParser
    {
        public static readonly CliArgument<string> SearchTermArgument = new("searchTerm")
        {
            HelpName = LocalizableStrings.SearchTermArgumentName,
            Description = LocalizableStrings.SearchTermDescription
        };

        public static readonly CliOption<bool> DetailOption = new("--detail")
        {
            Description = LocalizableStrings.DetailDescription
        };

        public static readonly CliOption<string> SkipOption = new("--skip")
        {
            Description = LocalizableStrings.SkipDescription,
            HelpName = LocalizableStrings.SkipArgumentName
        };

        public static readonly CliOption<string> TakeOption = new("--take")
        {
            Description = LocalizableStrings.TakeDescription,
            HelpName = LocalizableStrings.TakeArgumentName
        };

        public static readonly CliOption<bool> PrereleaseOption = new("--prerelease")
        {
            Description = LocalizableStrings.PrereleaseDescription
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("search", LocalizableStrings.CommandDescription);

            command.Arguments.Add(SearchTermArgument);

            command.Options.Add(DetailOption);
            command.Options.Add(SkipOption);
            command.Options.Add(TakeOption);
            command.Options.Add(PrereleaseOption);

            command.SetAction((parseResult) => new ToolSearchCommand(parseResult).Execute());

            return command;
        }
    }
}
