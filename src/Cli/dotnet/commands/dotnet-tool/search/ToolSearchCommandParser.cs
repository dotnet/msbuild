// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolSearchCommandParser
    {
        public static readonly Argument SearchTermArgument = new Argument(LocalizableStrings.SearchTermArgumentName)
        {
            Description = LocalizableStrings.SearchTermDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option DetailOption = new Option($"--detail", LocalizableStrings.DetailDescription);

        public static readonly Option SkipOption = new Option($"--skip", LocalizableStrings.SkipDescription) {
            Argument = new Argument(LocalizableStrings.SkipArgumentName)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        };

        public static readonly Option TakeOption = new Option($"--take", LocalizableStrings.TakeDescription)
        {
            Argument = new Argument(LocalizableStrings.TakeArgumentName)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        };

        public static readonly Option PrereleaseOption = new Option($"--prerelease", LocalizableStrings.PrereleaseDescription);

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
