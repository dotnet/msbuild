// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class CleanCommandParser
    {
        public static Command Clean() =>
            Create.Command("clean",
                           ".NET Clean Command",
                           CommonOptions.HelpOption(),
                           Create.Option("-o|--output", "Directory in which the build outputs have been placed.",
                                         Accept.ExactlyOneArgument
                                               .With(name: "OUTPUT_DIR")),
                           Create.Option("-f|--framework", "Clean a specific framework.",
                                         Accept.ExactlyOneArgument
                                               .With(name: "FRAMEWORK")
                                               .WithSuggestionsFrom(_ => Suggest.TargetFrameworksFromProjectFile())),
                           Create.Option("-c|--configuration",
                                         "Clean a specific configuration.",
                                         Accept.ExactlyOneArgument
                                               .With(name: "CONFIGURATION")
                                               .WithSuggestionsFrom("DEBUG", "RELEASE")));
    }
}