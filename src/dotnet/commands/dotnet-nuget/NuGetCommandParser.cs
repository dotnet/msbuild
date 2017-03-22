// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class NuGetCommandParser
    {
        public static Command NuGet() =>
            Create.Command("nuget",
                           "NuGet Command Line 4.0.0.0",
                           CommonOptions.HelpOption(),
                           Create.Option("--version",
                                         "Show version information"),
                           Create.Option("-v|--verbosity",
                                         "The verbosity of logging to use. Allowed values: Debug, Verbose, Information, Minimal, Warning, Error.",
                                         Accept.ExactlyOneArgument()
                                               .With(name: "verbosity")),
                           Create.Command("delete",
                                          "Deletes a package from the server.",
                                          Accept.ExactlyOneArgument()
                                                .With(name: "root",
                                                      description: "The Package Id and version."),
                                          CommonOptions.HelpOption(),
                                          Create.Option("--force-english-output",
                                                        "Forces the application to run using an invariant, English-based culture."),
                                          Create.Option("-s|--source",
                                                        "Specifies the server URL",
                                                        Accept.ExactlyOneArgument()
                                                              .With(name: "source")),
                                          Create.Option("--non-interactive",
                                                        "Do not prompt for user input or confirmations."),
                                          Create.Option("-k|--api-key",
                                                        "The API key for the server.",
                                                        Accept.ExactlyOneArgument()
                                                              .With(name: "apiKey"))),
                           Create.Command("locals",
                                          "Clears or lists local NuGet resources such as http requests cache, packages cache or machine-wide global packages folder.",
                                          Accept.AnyOneOf(@"all",
                                                          @"http-cache",
                                                          @"global-packages",
                                                          @"temp")
                                                .With(description: "Cache Location(s)  Specifies the cache location(s) to list or clear."),
                                          CommonOptions.HelpOption(),
                                          Create.Option("--force-english-output",
                                                        "Forces the application to run using an invariant, English-based culture."),
                                          Create.Option("-c|--clear", "Clear the selected local resources or cache location(s)."),
                                          Create.Option("-l|--list", "List the selected local resources or cache location(s).")),
                           Create.Command("push",
                                          "Pushes a package to the server and publishes it.",
                                          CommonOptions.HelpOption(),
                                          Create.Option("--force-english-output",
                                                        "Forces the application to run using an invariant, English-based culture."),
                                          Create.Option("-s|--source",
                                                        "Specifies the server URL",
                                                        Accept.ExactlyOneArgument()
                                                              .With(name: "source")),
                                          Create.Option("-ss|--symbol-source",
                                                        "Specifies the symbol server URL. If not specified, nuget.smbsrc.net is used when pushing to nuget.org.",
                                                        Accept.ExactlyOneArgument()
                                                              .With(name: "source")),
                                          Create.Option("-t|--timeout",
                                                        "Specifies the timeout for pushing to a server in seconds. Defaults to 300 seconds (5 minutes).",
                                                        Accept.ExactlyOneArgument()
                                                              .With(name: "timeout")),
                                          Create.Option("-k|--api-key", "The API key for the server.",
                                                        Accept.ExactlyOneArgument()
                                                              .With(name: "apiKey")),
                                          Create.Option("-sk|--symbol-api-key", "The API key for the symbol server.",
                                                        Accept.ExactlyOneArgument()
                                                              .With(name: "apiKey")),
                                          Create.Option("-d|--disable-buffering",
                                                        "Disable buffering when pushing to an HTTP(S) server to decrease memory usage."),
                                          Create.Option("-n|--no-symbols",
                                                        "If a symbols package exists, it will not be pushed to a symbols server.")));
    }
}