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
        public static readonly Argument SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly Option OuputOption = new ForwardedOption<string>(new string[] { "-o", "--output" }, LocalizableStrings.OutputOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.OutputOption
        }.ForwardAsSingle(o => $"-property:PublishDir={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option ManifestOption = new ForwardedOption<IEnumerable<string>>("--manifest", LocalizableStrings.ManifestOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.ManifestOption
        }.ForwardAsSingle(o => $"-property:TargetManifestFiles={string.Join("%3B", o.Select(CommandDirectoryContext.GetFullPath))}")
        .AllowSingleArgPerToken();

        public static readonly Option NoBuildOption = new ForwardedOption<bool>("--no-build", LocalizableStrings.NoBuildOptionDescription)
            .ForwardAs("-property:NoBuild=true");

        public static readonly Option SelfContainedOption = new ForwardedOption<bool>("--self-contained", LocalizableStrings.SelfContainedOptionDescription)
            .ForwardAsSingle(o =>  $"-property:SelfContained={o}");

        public static readonly Option NoSelfContainedOption = new ForwardedOption<bool>("--no-self-contained", LocalizableStrings.NoSelfContainedOptionDescription)
            .ForwardAs("-property:SelfContained=false");

        public static readonly Option NoLogoOption = new ForwardedOption<bool>("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs("-nologo");

        public static readonly Option NoRestoreOption = CommonOptions.NoRestoreOption();

        public static Command GetCommand()
        {
            var command = new Command("publish", LocalizableStrings.AppDescription);

            command.AddArgument(SlnOrProjectArgument);
            RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: false, includeNoDependenciesOption: true);
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
