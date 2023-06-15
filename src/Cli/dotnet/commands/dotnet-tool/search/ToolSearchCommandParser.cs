// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.Tool.Search;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolSearchCommandParser
    {
        public static readonly Argument<string> SearchTermArgument = new Argument<string>(LocalizableStrings.SearchTermArgumentName)
        {
            Description = LocalizableStrings.SearchTermDescription
        };

        public static readonly Option<bool> DetailOption = new Option<bool>("--detail", LocalizableStrings.DetailDescription);

        public static readonly Option<string> SkipOption = new Option<string>("--skip", LocalizableStrings.SkipDescription)
        {
            ArgumentHelpName = LocalizableStrings.SkipArgumentName
        };

        public static readonly Option<string> TakeOption = new Option<string>($"--take", LocalizableStrings.TakeDescription)
        {
            ArgumentHelpName = LocalizableStrings.TakeArgumentName
        };

        public static readonly Option<bool> PrereleaseOption = new Option<bool>($"--prerelease", LocalizableStrings.PrereleaseDescription);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("search", LocalizableStrings.CommandDescription);

            command.AddArgument(SearchTermArgument);

            command.AddOption(DetailOption);
            command.AddOption(SkipOption);
            command.AddOption(TakeOption);
            command.AddOption(PrereleaseOption);

            command.SetHandler((parseResult) => new ToolSearchCommand(parseResult).Execute());

            return command;
        }
    }
}
