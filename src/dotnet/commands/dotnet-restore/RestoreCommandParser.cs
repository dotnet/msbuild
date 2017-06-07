// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RestoreCommandParser
    {
        public static Command Restore() =>
            Create.Command(
                "restore",
                LocalizableStrings.AppFullName,
                Accept.ZeroOrMoreArguments(),
                CommonOptions.HelpOption(),
                Create.Option(
                    "-s|--source",
                    LocalizableStrings.CmdSourceOptionDescription,
                    Accept.OneOrMoreArguments()
                          .With(name: LocalizableStrings.CmdSourceOption)
                          .ForwardAsSingle(o => $"/p:RestoreSources={string.Join("%3B", o.Arguments)}")),
                Create.Option(
                    "-r|--runtime",
                    LocalizableStrings.CmdRuntimeOptionDescription,
                    Accept.OneOrMoreArguments()
                          .WithSuggestionsFrom(_ => Suggest.RunTimesFromProjectFile())
                          .With(name: LocalizableStrings.CmdRuntimeOption)
                          .ForwardAsSingle(o => $"/p:RuntimeIdentifiers={string.Join("%3B", o.Arguments)}")),
                Create.Option(
                    "--packages",
                    LocalizableStrings.CmdPackagesOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.CmdPackagesOption)
                          .ForwardAsSingle(o => $"/p:RestorePackagesPath={o.Arguments.Single()}")),
                Create.Option(
                    "--disable-parallel",
                    LocalizableStrings.CmdDisableParallelOptionDescription,
                    Accept.NoArguments()
                          .ForwardAs("/p:RestoreDisableParallel=true")),
                Create.Option(
                    "--configfile",
                    LocalizableStrings.CmdConfigFileOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.CmdConfigFileOption)
                          .ForwardAsSingle(o => $"/p:RestoreConfigFile={o.Arguments.Single()}")),
                Create.Option(
                    "--no-cache",
                    LocalizableStrings.CmdNoCacheOptionDescription,
                    Accept.NoArguments()
                          .ForwardAs("/p:RestoreNoCache=true")),
                Create.Option(
                    "--ignore-failed-sources",
                    LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription,
                    Accept.NoArguments()
                          .ForwardAs("/p:RestoreIgnoreFailedSources=true")),
                Create.Option(
                    "--no-dependencies",
                    LocalizableStrings.CmdNoDependenciesOptionDescription,
                    Accept.NoArguments()
                          .ForwardAs("/p:RestoreRecursive=false")),
                Create.Option(
                    "-f|--force",
                    LocalizableStrings.CmdForceRestoreOptionDescription,
                    Accept.NoArguments()
                          .ForwardAs("/p:RestoreForce=true")),
                CommonOptions.VerbosityOption());
    }
}