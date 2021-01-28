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
        public static readonly Argument Argument = new Argument<IEnumerable<string>>()
        {
            Arity = ArgumentArity.ZeroOrMore,
        };

        public static readonly Option ManifestOption = new Option<IEnumerable<string>>(new string[] { "-m", "--manifest" },
                    LocalizableStrings.ProjectManifestDescription)
        {
            Argument = new Argument<IEnumerable<string>>(LocalizableStrings.ProjectManifest)
            {
                Arity = ArgumentArity.OneOrMore
            }
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

        public static readonly Option FrameworkVersionOption = new Option<string>("--framework-version", LocalizableStrings.FrameworkVersionOptionDescription)
        {
            Argument = new Argument<string>(LocalizableStrings.FrameworkVersionOption)
        }.ForwardAsSingle(o => $"-property:RuntimeFrameworkVersion={o}");

        public static readonly Option OutputOption = new Option<string>(new string[] { "-o", "--output" }, LocalizableStrings.OutputOptionDescription)
        {
            Argument = new Argument<string>(LocalizableStrings.OutputOption)
        }.ForwardAsSingle(o => $"-property:ComposeDir={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option WorkingDirOption = new Option<string>(new string[] { "-w", "--working-dir" }, LocalizableStrings.IntermediateWorkingDirOptionDescription)
        {
            Argument = new Argument<string>(LocalizableStrings.IntermediateWorkingDirOption)
        }.ForwardAsSingle(o => $"-property:ComposeWorkingDir={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option SkipOptimizationOption = new Option<bool>("--skip-optimization", LocalizableStrings.SkipOptimizationOptionDescription)
            .ForwardAs("-property:SkipOptimization=true");

        public static readonly Option SkipSymbolsOption = new Option<bool>("--skip-symbols", LocalizableStrings.SkipSymbolsOptionDescription)
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
