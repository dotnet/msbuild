// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class NewCommandParser
    {
        public static Command New() =>
            Create.Command("new",
                           "Initialize .NET projects.",
                           Accept
                               .ExactlyOneArgument()
                               .WithSuggestionsFrom(
                                   "console",
                                   "classlib",
                                   "mstest",
                                   "xunit",
                                   "web",
                                   "mvc",
                                   "webapi",
                                   "sln"),
                           Create.Option("-l|--list",
                                         "List templates containing the specified name."),
                           Create.Option("-lang|--language",
                                         "Specifies the language of the template to create",
                                         Accept.WithSuggestionsFrom("C#", "F#")
                                               .With(defaultValue: () => "C#")),
                           Create.Option("-n|--name",
                                         "The name for the output being created. If no name is specified, the name of the current directory is used."),
                           Create.Option("-o|--output",
                                         "Location to place the generated output."),
                           Create.Option("-h|--help",
                                         "Displays help for this command."),
                           Create.Option("-all|--show-all",
                                         "Shows all templates"));
    }
}