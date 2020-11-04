// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Publish.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PublishCommandParser
    {
        public static readonly Argument SlnOrProjectArgument = new Argument(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly Option OuputOption = new Option(new string[] { "-o", "--output" }, LocalizableStrings.OutputOptionDescription)
        {
            Argument = new Argument(LocalizableStrings.OutputOption)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"-property:PublishDir={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option ManifestOption = new Option("--manifest", LocalizableStrings.ManifestOptionDescription)
        {
            Argument = new Argument(LocalizableStrings.ManifestOption)
            {
                Arity = ArgumentArity.OneOrMore
            }
        }.ForwardAsSingle<IEnumerable<string>>(o => $"-property:TargetManifestFiles={string.Join("%3B", o.Select(CommandDirectoryContext.GetFullPath))}");

        public static readonly Option NoBuildOption = new Option("--no-build", LocalizableStrings.NoBuildOptionDescription)
            .ForwardAs("-property:NoBuild=true");

        public static readonly Option SelfContainedOption = new Option("--self-contained", LocalizableStrings.SelfContainedOptionDescription)
        {
            Argument = new Argument()
            {
                Arity = ArgumentArity.ZeroOrOne
            }
        }.FromAmong(new string[] { "true", "false" })
        .ForwardAsSingle<bool>(o =>  $"-property:SelfContained={o}");

        public static readonly Option NoSelfContainedOption = new Option(
            "--no-self-contained",
            LocalizableStrings.NoSelfContainedOptionDescription)
            .ForwardAs("-property:SelfContained=false");

        public static readonly Option NoLogoOption = new Option(
            "--nologo",
            LocalizableStrings.CmdNoLogo)
            .ForwardAs("-nologo");

        public static readonly Option NoRestoreOption = CommonOptions.NoRestoreOption();

        public static Command GetCommand()
        {
            var command = new Command("publish", LocalizableStrings.AppDescription);

            command.AddArgument(SlnOrProjectArgument);
            command.AddOption(OuputOption);
            command.AddOption(ManifestOption);
            command.AddOption(NoBuildOption);
            command.AddOption(SelfContainedOption);
            command.AddOption(NoSelfContainedOption);
            command.AddOption(NoLogoOption);
            command.AddOption(CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription));
            command.AddOption(CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription));
            command.AddOption(CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription));
            command.AddOption(CommonOptions.VersionSuffixOption());
            command.AddOption(CommonOptions.InteractiveMsBuildForwardOption());
            command.AddOption(NoRestoreOption);
            command.AddOption(CommonOptions.VerbosityOption());

            return command;
        }
    }
}
