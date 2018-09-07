// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Store.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class StoreCommandParser
    {
        public static Command Store() =>
            Create.Command(
                "store",
                LocalizableStrings.AppDescription,
                Accept.ZeroOrMoreArguments(),
                CommonOptions.HelpOption(),
                Create.Option(
                    "-m|--manifest",
                    LocalizableStrings.ProjectManifestDescription,
                    Accept.OneOrMoreArguments()
                          .With(name: LocalizableStrings.ProjectManifest)
                          .ForwardAsMany(o =>
                          {
                              // the first path doesn't need to go through CommandDirectoryContext.ExpandPath
                              // since it is a direct argument to MSBuild, not a property
                              var materializedString = $"{o.Arguments.First()}";

                              if (o.Arguments.Count == 1)
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
                                      $"-property:AdditionalProjects={string.Join("%3B", o.Arguments.Skip(1).Select(CommandDirectoryContext.GetFullPath))}"
                                  };
                              }
                          })),
                CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription),
                Create.Option(
                    "--framework-version",
                    LocalizableStrings.FrameworkVersionOptionDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.FrameworkVersionOption)
                        .ForwardAsSingle(o => $"-property:RuntimeFrameworkVersion={o.Arguments.Single()}")),
                CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription),
                Create.Option(
                    "-o|--output",
                    LocalizableStrings.OutputOptionDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.OutputOption)
                        .ForwardAsSingle(o => $"-property:ComposeDir={CommandDirectoryContext.GetFullPath(o.Arguments.Single())}")),
                Create.Option(
                    "-w|--working-dir",
                    LocalizableStrings.IntermediateWorkingDirOptionDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.IntermediateWorkingDirOption)
                        .ForwardAsSingle(o => $"-property:ComposeWorkingDir={CommandDirectoryContext.GetFullPath(o.Arguments.Single())}")),
                Create.Option(
                    "--skip-optimization",
                    LocalizableStrings.SkipOptimizationOptionDescription,
                    Accept.NoArguments()
                          .ForwardAs("-property:SkipOptimization=true")),
                Create.Option(
                    "--skip-symbols",
                    LocalizableStrings.SkipSymbolsOptionDescription,
                    Accept.NoArguments()
                          .ForwardAs("-property:CreateProfilingSymbols=false")),
                CommonOptions.VerbosityOption());
    }
}
