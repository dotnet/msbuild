// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    // This parser is used for completion and telemetry.
    // See https://github.com/NuGet/NuGet.Client for the actual implementation.
    internal static class NuGetCommandParser
    {
        public static Command NuGet() =>
            Create.Command(
                "nuget",
                Parser.CompletionOnlyDescription,
                Create.Option("-h|--help", Parser.CompletionOnlyDescription),
                Create.Option("--version", Parser.CompletionOnlyDescription),
                Create.Option("-v|--verbosity", Parser.CompletionOnlyDescription, Accept.ExactlyOneArgument()),
                Create.Command(
                    "delete",
                    Parser.CompletionOnlyDescription,
                    Accept.OneOrMoreArguments(),
                    Create.Option("-h|--help", Parser.CompletionOnlyDescription),
                    Create.Option("--force-english-output", Parser.CompletionOnlyDescription),
                    Create.Option("-s|--source", Parser.CompletionOnlyDescription, Accept.ExactlyOneArgument()),
                    Create.Option("--non-interactive", Parser.CompletionOnlyDescription),
                    Create.Option("-k|--api-key", Parser.CompletionOnlyDescription, Accept.ExactlyOneArgument()),
                    Create.Option("--no-service-endpoint", Parser.CompletionOnlyDescription),
                    Create.Option("--interactive", Parser.CompletionOnlyDescription)),
                Create.Command(
                    "locals",
                    Parser.CompletionOnlyDescription,
                    Accept.AnyOneOf(
                        "all",
                        "http-cache",
                        "global-packages",
                        "plugins-cache",
                        "temp"),
                    Create.Option("-h|--help", Parser.CompletionOnlyDescription),
                    Create.Option("--force-english-output", Parser.CompletionOnlyDescription),
                    Create.Option("-c|--clear", Parser.CompletionOnlyDescription),
                    Create.Option("-l|--list", Parser.CompletionOnlyDescription)),
                Create.Command(
                    "push",
                    Parser.CompletionOnlyDescription,
                    Accept.OneOrMoreArguments(),
                    Create.Option("-h|--help", Parser.CompletionOnlyDescription),
                    Create.Option("--force-english-output", Parser.CompletionOnlyDescription),
                    Create.Option("-s|--source", Parser.CompletionOnlyDescription, Accept.ExactlyOneArgument()),
                    Create.Option("-ss|--symbol-source", Parser.CompletionOnlyDescription, Accept.ExactlyOneArgument()),
                    Create.Option("-t|--timeout", Parser.CompletionOnlyDescription, Accept.ExactlyOneArgument()),
                    Create.Option("-k|--api-key", Parser.CompletionOnlyDescription, Accept.ExactlyOneArgument()),
                    Create.Option("-sk|--symbol-api-key", Parser.CompletionOnlyDescription, Accept.ExactlyOneArgument()),
                    Create.Option("-d|--disable-buffering", Parser.CompletionOnlyDescription),
                    Create.Option("-n|--no-symbols", Parser.CompletionOnlyDescription),
                    Create.Option("--no-service-endpoint", Parser.CompletionOnlyDescription),
                    Create.Option("--interactive", Parser.CompletionOnlyDescription)
                    ));
    }
}
