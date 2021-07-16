// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using LocalizableStrings = Microsoft.DotNet.Tools.Store.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class StoreCommandParser
    {
        public static readonly Argument<IEnumerable<string>> Argument = new Argument<IEnumerable<string>>()
        {
            Arity = ArgumentArity.ZeroOrMore,
        };

        public static readonly Option<IEnumerable<string>> ManifestOption = new ForwardedOption<IEnumerable<string>>(new string[] { "-m", "--manifest" },
                    LocalizableStrings.ProjectManifestDescription)
        {
            ArgumentHelpName = LocalizableStrings.ProjectManifest
        }.ForwardAsMany(o => {
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

        public static readonly Option<string> FrameworkVersionOption = new ForwardedOption<string>("--framework-version", LocalizableStrings.FrameworkVersionOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.FrameworkVersionOption
        }.ForwardAsSingle(o => $"-property:RuntimeFrameworkVersion={o}");

        public static readonly Option<string> OutputOption = new ForwardedOption<string>(new string[] { "-o", "--output" }, LocalizableStrings.OutputOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.OutputOption
        }.ForwardAsSingle(o => $"-property:ComposeDir={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option<string> WorkingDirOption = new ForwardedOption<string>(new string[] { "-w", "--working-dir" }, LocalizableStrings.IntermediateWorkingDirOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.IntermediateWorkingDirOption
        }.ForwardAsSingle(o => $"-property:ComposeWorkingDir={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option<bool> SkipOptimizationOption = new ForwardedOption<bool>("--skip-optimization", LocalizableStrings.SkipOptimizationOptionDescription)
            .ForwardAs("-property:SkipOptimization=true");

        public static readonly Option<bool> SkipSymbolsOption = new ForwardedOption<bool>("--skip-symbols", LocalizableStrings.SkipSymbolsOptionDescription)
            .ForwardAs("-property:CreateProfilingSymbols=false");

        public static Command GetCommand()
        {
            var command = new Command("store", LocalizableStrings.AppDescription);

            command.AddArgument(Argument);
            command.AddOption(ManifestOption);
            command.AddOption(FrameworkVersionOption);
            command.AddOption(OutputOption);
            command.AddOption(WorkingDirOption);
            command.AddOption(SkipOptimizationOption);
            command.AddOption(SkipSymbolsOption);
            command.AddOption(CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription));
            command.AddOption(CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription));
            command.AddOption(CommonOptions.VerbosityOption());
			command.AddOption(CommonOptions.CurrentRuntimeOption(LocalizableStrings.CurrentRuntimeOptionDescription));

            return command;
        }
    }
}
