// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Cache.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class CacheCommandParser
    {
        public static Command Cache() =>
            Create.Command(
                "cache",
                LocalizableStrings.AppDescription,
                Accept.ZeroOrMoreArguments(),
                CommonOptions.HelpOption(),
                Create.Option(
                    "-e|--entries",
                    LocalizableStrings.ProjectEntryDescription,
                    Accept.OneOrMoreArguments()
                        .With(name: LocalizableStrings.ProjectEntries)
                        .ForwardAs(o => 
                        {
                            var materializedString = $"{o.Arguments.First()}";

                            if (o.Arguments.Count() == 1) return materializedString;

                            var additionalProjects = string.Join("%3B", o.Arguments.Skip(1));

                            return $"{materializedString} /p:AdditionalProjects={additionalProjects}";
                        })),
                CommonOptions.FrameworkOption(),
                Create.Option(
                    "--framework-version",
                    LocalizableStrings.FrameworkVersionOptionDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.FrameworkVersionOption)
                        .ForwardAs(o => $"/p:FX_Version={o.Arguments.Single()}")),
                CommonOptions.RuntimeOption(),
                CommonOptions.ConfigurationOption(),
                Create.Option(
                    "-o|--output",
                    LocalizableStrings.OutputOptionDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.OutputOption)
                        .ForwardAs(o => $"/p:ComposeDir={o.Arguments.Single()}")),
                Create.Option(
                    "-w|--working-dir",
                    LocalizableStrings.IntermediateWorkingDirOptionDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.IntermediateWorkingDirOption)
                        .ForwardAs(o => $"/p:ComposeWorkingDir={o.Arguments.Single()}")),
                Create.Option(
                    "--preserve-working-dir",
                    LocalizableStrings.PreserveIntermediateWorkingDirOptionDescription,
                    Accept.NoArguments()
                        .ForwardAs(o => $"/p:PreserveComposeWorkingDir=true")),
                Create.Option(
                    "--skip-optimization",
                    LocalizableStrings.SkipOptimizationOptionDescription,
                    Accept.NoArguments()
                          .ForwardAs("/p:SkipOptimization=true")),
                CommonOptions.VerbosityOption());
    }
}