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
                Accept.OneOrMoreArguments,
                CommonOptions.HelpOption(),
                Create.Option(
                    "-s|--source",
                    "Specifies a NuGet package source to use during the restore.",
                    Accept.OneOrMoreArguments
                          .With(name: "SOURCE")
                          .ForwardAs(o => $"/p:RestoreSources={string.Join("%3B", o.Arguments)}")),
                Create.Option(
                    "-r|--runtime",
                    "Target runtime to restore packages for.",
                    Accept.OneOrMoreArguments
                          .WithSuggestionsFrom(_ => Suggest.RunTimesFromProjectFile())
                          .With(name: "RUNTIME_IDENTIFIER")
                          .ForwardAs(o => $"/p:RuntimeIdentifiers={string.Join("%3B", o.Arguments)}")),
                Create.Option(
                    "--packages",
                    "Directory to install packages in.",
                    Accept.ExactlyOneArgument
                          .With(name: "PACKAGES_DIRECTORY")
                          .ForwardAs("/p:RestorePackagesPath={0}")),
                Create.Option(
                    "--disable-parallel",
                    "Disables restoring multiple projects in parallel.",
                    Accept.NoArguments
                          .ForwardAs("/p:RestoreDisableParallel=true")),
                Create.Option(
                    "--configfile",
                    "The NuGet configuration file to use.",
                    Accept.ExactlyOneArgument
                          .With(name: "FILE")
                          .ForwardAs("/p:RestoreConfigFile={0}")),
                Create.Option(
                    "--no-cache",
                    "Do not cache packages and http requests.",
                    Accept.NoArguments
                          .ForwardAs("/p:RestoreNoCache=true")),
                Create.Option(
                    "--ignore-failed-sources",
                    "Treat package source failures as warnings.",
                    Accept.NoArguments
                          .ForwardAs("/p:RestoreIgnoreFailedSources=true")),
                Create.Option(
                    "--no-dependencies",
                    "Set this flag to ignore project to project references and only restore the root project",
                    Accept.NoArguments
                          .ForwardAs("/p:RestoreRecursive=false")),
                CommonOptions.VerbosityOption());
    }
}