// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RestoreCommandParser
    {
        public static readonly Argument SlnOrProjectArgument = new Argument(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        private static Option[] FullRestoreOptions() => 
            ImplicitRestoreOptions(true, true, true, true).Concat(
                new Option[] {
                    CommonOptions.VerbosityOption(),
                    CommonOptions.InteractiveMsBuildForwardOption(),
                    new Option(
                        "--use-lock-file",
                        LocalizableStrings.CmdUseLockFileOptionDescription)
                            .ForwardAs("-property:RestorePackagesWithLockFile=true"),
                    new Option(
                        "--locked-mode",
                        LocalizableStrings.CmdLockedModeOptionDescription)
                            .ForwardAs("-property:RestoreLockedMode=true"),
                    new Option(
                        "--lock-file-path",
                        LocalizableStrings.CmdLockFilePathOptionDescription)
                    {
                        Argument = new Argument(LocalizableStrings.CmdLockFilePathOption)
                        {
                            Arity = ArgumentArity.ExactlyOne
                        }
                    }.ForwardAsSingle<string>(o => $"-property:NuGetLockFilePath={o}"),
                    new Option(
                        "--force-evaluate",
                        LocalizableStrings.CmdReevaluateOptionDescription)
                            .ForwardAs("-property:RestoreForceEvaluate=true") })
                .ToArray();

        public static Command GetCommand()
        {
            var command = new Command("restore", LocalizableStrings.AppFullName);

            command.AddArgument(SlnOrProjectArgument);

            foreach (var option in FullRestoreOptions())
            {
                command.AddOption(option);
            }

            return command;
        }

        public static void AddImplicitRestoreOptions(Command command, bool showHelp = false, bool useShortOptions = false, bool includeRuntimeOption = true, bool includeNoDependenciesOption = true)
        {
            foreach (var option in ImplicitRestoreOptions(showHelp, useShortOptions, includeRuntimeOption, includeNoDependenciesOption))
            {
                command.AddOption(option);
            }
        }

        private static Option[] ImplicitRestoreOptions(bool showHelp, bool useShortOptions, bool includeRuntimeOption, bool includeNoDependenciesOption)
        {
            var options = new Option[] {
                new Option(
                    useShortOptions ? new string[] {"-s", "--source" }  : new string[] { "--source" },
                    showHelp ? LocalizableStrings.CmdSourceOptionDescription : string.Empty)
                {
                    Argument = new Argument(LocalizableStrings.CmdSourceOption) { Arity = ArgumentArity.OneOrMore },
                    IsHidden = !showHelp
                }.ForwardAsSingle<IEnumerable<string>>(o => $"-property:RestoreSources={string.Join("%3B", o)}"),
                new Option(
                    "--packages",
                    showHelp ? LocalizableStrings.CmdPackagesOptionDescription : string.Empty)
                {
                    Argument = new Argument(LocalizableStrings.CmdPackagesOption) { Arity = ArgumentArity.ExactlyOne },
                    IsHidden = !showHelp
                }.ForwardAsSingle<string>(o => $"-property:RestorePackagesPath={CommandDirectoryContext.GetFullPath(o)}"),
                new Option(
                    "--disable-parallel",
                    showHelp ? LocalizableStrings.CmdDisableParallelOptionDescription : string.Empty)
                {
                    IsHidden = !showHelp
                }.ForwardAs("-property:RestoreDisableParallel=true"),
                new Option(
                    "--configfile",
                    showHelp ? LocalizableStrings.CmdConfigFileOptionDescription : string.Empty)
                {
                    Argument = new Argument(LocalizableStrings.CmdConfigFileOption) { Arity = ArgumentArity.ExactlyOne },
                    IsHidden = !showHelp
                }.ForwardAsSingle<string>(o => $"-property:RestoreConfigFile={CommandDirectoryContext.GetFullPath(o)}"),
                new Option(
                    "--no-cache",
                    showHelp ? LocalizableStrings.CmdNoCacheOptionDescription : string.Empty)
                {
                    IsHidden = !showHelp
                }.ForwardAs("-property:RestoreNoCache=true"),
                new Option(
                    "--ignore-failed-sources",
                    showHelp ? LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription : string.Empty)
                {
                    IsHidden = !showHelp
                }.ForwardAs("-property:RestoreIgnoreFailedSources=true"),
                new Option(
                    useShortOptions ? new string[] {"-f", "--force" } : new string[] {"--force" },
                    LocalizableStrings.CmdForceRestoreOptionDescription)
                {
                    IsHidden = !showHelp
                }.ForwardAs("-property:RestoreForce=true")
            };

            if (includeRuntimeOption)
            {
                options = options.Append(
                    new Option(
                        useShortOptions ? new string[] { "-r", "--runtime" } : new string[] { "--runtime" },
                        LocalizableStrings.CmdRuntimeOptionDescription)
                    {
                        Argument = new Argument(LocalizableStrings.CmdRuntimeOption) { Arity = ArgumentArity.OneOrMore },
                        IsHidden = !showHelp
                    }.ForwardAsSingle<IEnumerable<string>>(o => $"-property:RuntimeIdentifiers={string.Join("%3B", o)}")
                    .AddSuggestions(Suggest.RunTimesFromProjectFile().ToArray())
                ).ToArray();
            }

            if (includeNoDependenciesOption)
            {
                options = options.Append(
                    new Option(
                        "--no-dependencies",
                        LocalizableStrings.CmdNoDependenciesOptionDescription)
                    {
                        IsHidden = !showHelp
                    }.ForwardAs("-property:RestoreRecursive=false")
                ).ToArray();
            }

            return options;
        }
    }
}
