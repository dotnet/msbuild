// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Restore;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RestoreCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-restore";

        public static readonly CliArgument<IEnumerable<string>> SlnOrProjectArgument = new CliArgument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly CliOption<IEnumerable<string>> SourceOption = new ForwardedOption<IEnumerable<string>>("--source", "-s")
        {
            Description = LocalizableStrings.CmdSourceOptionDescription,
            HelpName = LocalizableStrings.CmdSourceOption
        }.ForwardAsSingle(o => $"-property:RestoreSources={string.Join("%3B", o)}")
        .AllowSingleArgPerToken();

        private static IEnumerable<CliOption> FullRestoreOptions() => 
            ImplicitRestoreOptions(true, true, true, true).Concat(
                new CliOption[] {
                    CommonOptions.VerbosityOption,
                    CommonOptions.InteractiveMsBuildForwardOption,
                    new ForwardedOption<bool>("--use-lock-file")
                    {
                        Description = LocalizableStrings.CmdUseLockFileOptionDescription,
                    }.ForwardAs("-property:RestorePackagesWithLockFile=true"),
                    new ForwardedOption<bool>("--locked-mode")
                    {
                        Description = LocalizableStrings.CmdLockedModeOptionDescription
                    }.ForwardAs("-property:RestoreLockedMode=true"),
                    new ForwardedOption<string>("--lock-file-path")
                    {
                        Description = LocalizableStrings.CmdLockFilePathOptionDescription,
                        HelpName = LocalizableStrings.CmdLockFilePathOption
                    }.ForwardAsSingle(o => $"-property:NuGetLockFilePath={o}"),
                    new ForwardedOption<bool>("--force-evaluate")
                    {
                        Description = LocalizableStrings.CmdReevaluateOptionDescription
                    }.ForwardAs("-property:RestoreForceEvaluate=true") });

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new DocumentedCommand("restore", DocsLink, LocalizableStrings.AppFullName);

            command.Arguments.Add(SlnOrProjectArgument);
            command.Options.Add(CommonOptions.DisableBuildServersOption);

            foreach (var option in FullRestoreOptions())
            {
                command.Options.Add(option);
            }

            command.Options.Add(CommonOptions.ArchitectureOption);
            command.SetAction(RestoreCommand.Run);

            return command;
        }

        public static void AddImplicitRestoreOptions(CliCommand command, bool showHelp = false, bool useShortOptions = false, bool includeRuntimeOption = true, bool includeNoDependenciesOption = true)
        {
            foreach (var option in ImplicitRestoreOptions(showHelp, useShortOptions, includeRuntimeOption, includeNoDependenciesOption))
            {
                command.Options.Add(option);
            }
        }
        private static string GetOsFromRid(string rid) => rid.Substring(0, rid.LastIndexOf("-"));
        private static string GetArchFromRid(string rid) => rid.Substring(rid.LastIndexOf("-") + 1, rid.Length - rid.LastIndexOf("-") - 1);
        public static string RestoreRuntimeArgFunc(IEnumerable<string> rids) 
        {
            List<string> convertedRids = new();
            foreach (string rid in rids)
            {
                if (GetArchFromRid(rid.ToString()) == "amd64")
                {
                    convertedRids.Add($"{GetOsFromRid(rid.ToString())}-x64");
                }
                else
                {
                    convertedRids.Add($"{rid}");
                }
            }
            return $"-property:RuntimeIdentifiers={string.Join("%3B", convertedRids)}";
        }

        private static IEnumerable<CliOption> ImplicitRestoreOptions(bool showHelp, bool useShortOptions, bool includeRuntimeOption, bool includeNoDependenciesOption)
        {
            if (showHelp && useShortOptions)
            {
                yield return SourceOption;
            }
            else
            {
                CliOption<IEnumerable<string>> sourceOption = new ForwardedOption<IEnumerable<string>>("--source")
                {
                    Description = showHelp ? LocalizableStrings.CmdSourceOptionDescription : string.Empty,
                    HelpName = LocalizableStrings.CmdSourceOption,
                    Hidden = !showHelp
                }.ForwardAsSingle(o => $"-property:RestoreSources={string.Join("%3B", o)}") // '%3B' corresponds to ';'
                .AllowSingleArgPerToken();

                if (useShortOptions)
                {
                    sourceOption.Aliases.Add("-s");
                }

                yield return sourceOption;
            }

            yield return new ForwardedOption<string>("--packages")
            {
                Description = showHelp ? LocalizableStrings.CmdPackagesOptionDescription : string.Empty,
                HelpName = LocalizableStrings.CmdPackagesOption,
                Hidden = !showHelp
            }.ForwardAsSingle(o => $"-property:RestorePackagesPath={CommandDirectoryContext.GetFullPath(o)}");

            yield return CommonOptions.CurrentRuntimeOption(LocalizableStrings.CmdCurrentRuntimeOptionDescription);

            yield return new ForwardedOption<bool>("--disable-parallel")
            {
                Description = showHelp ? LocalizableStrings.CmdDisableParallelOptionDescription : string.Empty,
                Hidden = !showHelp
            }.ForwardAs("-property:RestoreDisableParallel=true");

            yield return new ForwardedOption<string>("--configfile")
            {
                Description = showHelp ? LocalizableStrings.CmdConfigFileOptionDescription : string.Empty,
                HelpName = LocalizableStrings.CmdConfigFileOption,
                Hidden = !showHelp
            }.ForwardAsSingle(o => $"-property:RestoreConfigFile={CommandDirectoryContext.GetFullPath(o)}");

            yield return new ForwardedOption<bool>("--no-cache")
            {
                Description = showHelp ? LocalizableStrings.CmdNoCacheOptionDescription : string.Empty,
                Hidden = !showHelp
            }.ForwardAs("-property:RestoreNoCache=true");

            yield return new ForwardedOption<bool>("--ignore-failed-sources")
            {
                Description = showHelp ? LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription : string.Empty,
                Hidden = !showHelp
            }.ForwardAs("-property:RestoreIgnoreFailedSources=true");

            ForwardedOption<bool> forceOption = new ForwardedOption<bool>("--force")
            {
                Description = LocalizableStrings.CmdForceRestoreOptionDescription,
                Hidden = !showHelp
            }.ForwardAs("-property:RestoreForce=true");
            if (useShortOptions)
            {
                forceOption.Aliases.Add("-f");
            }
            yield return forceOption;

            yield return CommonOptions.PropertiesOption;

            if (includeRuntimeOption)
            {
                CliOption<IEnumerable<string>> runtimeOption = new ForwardedOption<IEnumerable<string>>("--runtime")
                {
                    Description = LocalizableStrings.CmdRuntimeOptionDescription,
                    HelpName = LocalizableStrings.CmdRuntimeOption,
                    Hidden = !showHelp,
                }.ForwardAsSingle(RestoreRuntimeArgFunc)
                 .AllowSingleArgPerToken()
                 .AddCompletions(Complete.RunTimesFromProjectFile);

                if (useShortOptions)
                {
                    runtimeOption.Aliases.Add("-r");
                }

                yield return runtimeOption;
            }

            if (includeNoDependenciesOption)
            {
                yield return new ForwardedOption<bool>("--no-dependencies")
                {
                    Description = LocalizableStrings.CmdNoDependenciesOptionDescription,
                    Hidden = !showHelp
                }.ForwardAs("-property:RestoreRecursive=false");
            }
        }
    }
}
