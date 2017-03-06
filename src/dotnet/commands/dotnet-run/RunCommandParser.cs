// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class RunCommandParser
    {
        public static Command Run() =>
            Create.Command("run",
                           ".NET Run Command",
                           CommonOptions.HelpOption(),
                           Create.Option("-c|--configuration",
                                         @"Configuration to use for building the project. Default for most projects is ""Debug"".",
                                         Accept.ExactlyOneArgument
                                               .WithSuggestionsFrom("DEBUG", "RELEASE")),
                           Create.Option("-f|--framework",
                                         "Build and run the app using the specified framework. The framework has to be specified in the project file.",
                                         Accept.AnyOneOf(Suggest.TargetFrameworksFromProjectFile)),
                           Create.Option("-p|--project",
                                         "The path to the project file to run (defaults to the current directory if there is only one project).",
                                         Accept.ZeroOrOneArgument));
    }
}