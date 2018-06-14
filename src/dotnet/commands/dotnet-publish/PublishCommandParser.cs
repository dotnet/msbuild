// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Publish.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PublishCommandParser
    {
        public static Command Publish() =>
            CreateWithRestoreOptions.Command(
                "publish",
                LocalizableStrings.AppDescription,
                Accept.ZeroOrMoreArguments()
                      .With(name: CommonLocalizableStrings.ProjectArgumentName,
                            description: CommonLocalizableStrings.ProjectArgumentDescription),
                CommonOptions.HelpOption(),
                Create.Option(
                    "-o|--output",
                    LocalizableStrings.OutputOptionDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.OutputOption)
                        .ForwardAsSingle(o => $"-property:PublishDir={o.Arguments.Single()}")),
                CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription),
                CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription),
                CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription),
                CommonOptions.VersionSuffixOption(),
                Create.Option(
                    "--manifest",
                    LocalizableStrings.ManifestOptionDescription,
                    Accept.OneOrMoreArguments()
                        .With(name: LocalizableStrings.ManifestOption)
                        .ForwardAsSingle(o => $"-property:TargetManifestFiles={string.Join("%3B", o.Arguments)}")),
                Create.Option(
                    "--no-build",
                    LocalizableStrings.NoBuildOptionDescription,
                    Accept.NoArguments().ForwardAs("-property:NoBuild=true")),
                Create.Option(
                    "--self-contained",
                    LocalizableStrings.SelfContainedOptionDescription,
                    Accept.ZeroOrOneArgument()
                        .WithSuggestionsFrom("true", "false")
                        .ForwardAsSingle(o =>
                        {
                            string value = o.Arguments.Any() ? o.Arguments.Single() : "true";
                            return $"-property:SelfContained={value}";
                        })),
                CommonOptions.NoRestoreOption(),
                CommonOptions.VerbosityOption());
    }
}
