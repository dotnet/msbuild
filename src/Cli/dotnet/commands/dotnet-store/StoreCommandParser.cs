// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Store;
using LocalizableStrings = Microsoft.DotNet.Tools.Store.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class StoreCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-store";

        public static readonly CliArgument<IEnumerable<string>> Argument = new("argument")
        {
            Arity = ArgumentArity.ZeroOrMore,
        };

        public static readonly CliOption<IEnumerable<string>> ManifestOption = new ForwardedOption<IEnumerable<string>>("--manifest", "-m")
        {
            Description = LocalizableStrings.ProjectManifestDescription,
            HelpName = LocalizableStrings.ProjectManifest
        }.ForwardAsMany(o =>
        {
            // the first path doesn't need to go through CommandDirectoryContext.ExpandPath
            // since it is a direct argument to MSBuild, not a property
            var materializedString = $"{o.First()}";

            if (o.Count() == 1)
            {
                return new[]
                {
                    materializedString
                };
            }
            else
            {
                return new[]
                {
                    materializedString,
                    $"-property:AdditionalProjects={string.Join("%3B", o.Skip(1).Select(CommandDirectoryContext.GetFullPath))}"
                };
            }
        }).AllowSingleArgPerToken();

        public static readonly CliOption<string> FrameworkVersionOption = new ForwardedOption<string>("--framework-version")
        {
            Description = LocalizableStrings.FrameworkVersionOptionDescription,
            HelpName = LocalizableStrings.FrameworkVersionOption
        }.ForwardAsSingle(o => $"-property:RuntimeFrameworkVersion={o}");

        public static readonly CliOption<string> OutputOption = new ForwardedOption<string>("--output", "-o")
        {
            Description = LocalizableStrings.OutputOptionDescription,
            HelpName = LocalizableStrings.OutputOption
        }.ForwardAsOutputPath("ComposeDir");

        public static readonly CliOption<string> WorkingDirOption = new ForwardedOption<string>("--working-dir", "-w")
        {
            Description = LocalizableStrings.IntermediateWorkingDirOptionDescription,
            HelpName = LocalizableStrings.IntermediateWorkingDirOption
        }.ForwardAsSingle(o => $"-property:ComposeWorkingDir={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly CliOption<bool> SkipOptimizationOption = new ForwardedOption<bool>("--skip-optimization")
        {
            Description = LocalizableStrings.SkipOptimizationOptionDescription
        }.ForwardAs("-property:SkipOptimization=true");

        public static readonly CliOption<bool> SkipSymbolsOption = new ForwardedOption<bool>("--skip-symbols")
        {
            Description = LocalizableStrings.SkipSymbolsOptionDescription
        }.ForwardAs("-property:CreateProfilingSymbols=false");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new("store", DocsLink, LocalizableStrings.AppDescription);

            command.Arguments.Add(Argument);
            command.Options.Add(ManifestOption);
            command.Options.Add(FrameworkVersionOption);
            command.Options.Add(OutputOption);
            command.Options.Add(WorkingDirOption);
            command.Options.Add(SkipOptimizationOption);
            command.Options.Add(SkipSymbolsOption);
            command.Options.Add(CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription));
            command.Options.Add(CommonOptions.RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.Options.Add(CommonOptions.VerbosityOption);
            command.Options.Add(CommonOptions.CurrentRuntimeOption(LocalizableStrings.CurrentRuntimeOptionDescription));
            command.Options.Add(CommonOptions.DisableBuildServersOption);

            command.SetAction(StoreCommand.Run);

            return command;
        }
    }
}
