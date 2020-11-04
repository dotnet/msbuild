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
        public static readonly Argument Argument = new Argument()
        {
            Arity = ArgumentArity.ZeroOrMore,
        };

        public static readonly Option ManifestOption = new Option(new string[] { "-m", "--manifest" },
                    LocalizableStrings.ProjectManifestDescription)
        {
            Argument = new Argument(LocalizableStrings.ProjectManifest)
            {
                Arity = ArgumentArity.OneOrMore
            }
        }.ForwardAsMany<IReadOnlyCollection<string>>(o => {
            // the first path doesn't need to go through CommandDirectoryContext.ExpandPath
            // since it is a direct argument to MSBuild, not a property
            var materializedString = $"{o.First()}";

            if (o.Count == 1)
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
        });

        public static readonly Option FrameworkVersionOption = new Option("--framework-version", LocalizableStrings.FrameworkVersionOptionDescription)
        {
            Argument = new Argument(LocalizableStrings.FrameworkVersionOption)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"-property:RuntimeFrameworkVersion={o}");

        public static readonly Option OutputOption = new Option(new string[] { "-o", "--output" }, LocalizableStrings.OutputOptionDescription)
        {
            Argument = new Argument(LocalizableStrings.OutputOption)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"-property:ComposeDir={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option WorkingDirOption = new Option(new string[] { "-w", "--working-dir" }, LocalizableStrings.IntermediateWorkingDirOptionDescription)
        {
            Argument = new Argument(LocalizableStrings.IntermediateWorkingDirOption)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"-property:ComposeWorkingDir={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option SkipOptimizationOption = new Option("--skip-optimization", LocalizableStrings.SkipOptimizationOptionDescription)
            .ForwardAs("-property:SkipOptimization=true");

        public static readonly Option SkipSymbolsOption = new Option("--skip-symbols", LocalizableStrings.SkipSymbolsOptionDescription)
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

            return command;
        }
    }
}
