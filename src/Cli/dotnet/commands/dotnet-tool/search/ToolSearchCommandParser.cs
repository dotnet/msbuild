// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
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

        public static Command GetCommand()
        {
            var command = new Command("search", LocalizableStrings.CommandDescription);

            command.AddArgument(SearchTermArgument);

            command.AddOption(DetailOption);
            command.AddOption(SkipOption);
            command.AddOption(TakeOption);
            command.AddOption(PrereleaseOption);

            return command;
        }
    }
}
