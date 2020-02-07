// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
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
                      .With(name: CommonLocalizableStrings.SolutionOrProjectArgumentName,
                            description: CommonLocalizableStrings.SolutionOrProjectArgumentDescription),
                CommonOptions.HelpOption(),
                Create.Option(
                    "-o|--output",
                    LocalizableStrings.OutputOptionDescription,
                    Accept.ExactlyOneArgument()
                        .With(name: LocalizableStrings.OutputOption)
                        .ForwardAsSingle(o => $"-property:PublishDir={CommandDirectoryContext.GetFullPath(o.Arguments.Single())}")),
                CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription),
                CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription),
                CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription),
                CommonOptions.VersionSuffixOption(),
                Create.Option(
                    "--manifest",
                    LocalizableStrings.ManifestOptionDescription,
                    Accept.OneOrMoreArguments()
                        .With(name: LocalizableStrings.ManifestOption)
                        .ForwardAsSingle(o => $"-property:TargetManifestFiles={string.Join("%3B", o.Arguments.Select(CommandDirectoryContext.GetFullPath))}")),
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
                Create.Option(
                    "--no-self-contained",
                    LocalizableStrings.NoSelfContainedOptionDescription,
                    Accept.NoArguments().ForwardAs("-property:SelfContained=false")),
                Create.Option(
                    "--nologo|/nologo",
                    LocalizableStrings.CmdNoLogo,
                    Accept.NoArguments()
                          .ForwardAs("-nologo")),
                CommonOptions.InteractiveMsBuildForwardOption(),
                CommonOptions.NoRestoreOption(),
                CommonOptions.VerbosityOption());
    }
}
