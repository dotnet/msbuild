// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class RestoreCommandParser
    {
        public static Command Restore() =>
            Create.Command("restore",
                           ".NET dependency restorer",
                           Accept.ZeroOrOneArgument,
                           CommonOptions.HelpOption(),
                           Create.Option("-s|--source",
                                         "Specifies a NuGet package source to use during the restore.",
                                         Accept.ExactlyOneArgument
                                               .With(name: "SOURCE")),
                           Create.Option("-r|--runtime",
                                         "Target runtime to restore packages for.",
                                         Accept.AnyOneOf(Suggest.RunTimesFromProjectFile)
                                               .With(name: "RUNTIME_IDENTIFIER")),
                           Create.Option("--packages",
                                         "Directory to install packages in.",
                                         Accept.ExactlyOneArgument
                                               .With(name: "PACKAGES_DIRECTORY")),
                           Create.Option("--disable-parallel",
                                         "Disables restoring multiple projects in parallel."),
                           Create.Option("--configfile",
                                         "The NuGet configuration file to use.",
                                         Accept.ExactlyOneArgument
                                               .With(name: "FILE")),
                           Create.Option("--no-cache",
                                         "Do not cache packages and http requests."),
                           Create.Option("--ignore-failed-sources",
                                         "Treat package source failures as warnings."),
                           Create.Option("--no-dependencies",
                                         "Set this flag to ignore project to project references and only restore the root project"),
                           CommonOptions.VerbosityOption());
    }
}