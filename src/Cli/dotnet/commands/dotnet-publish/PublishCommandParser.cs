// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Publish;
using LocalizableStrings = Microsoft.DotNet.Tools.Publish.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PublishCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-publish";

        public static readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly Option<string> OuputOption = new ForwardedOption<string>(new string[] { "-o", "--output" }, LocalizableStrings.OutputOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.OutputOption
        }.ForwardAsOutputPath("PublishDir");

        public static readonly Option<IEnumerable<string>> ManifestOption = new ForwardedOption<IEnumerable<string>>("--manifest", LocalizableStrings.ManifestOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.ManifestOption
        }.ForwardAsSingle(o => $"-property:TargetManifestFiles={string.Join("%3B", o.Select(CommandDirectoryContext.GetFullPath))}")
        .AllowSingleArgPerToken();

        public static readonly Option<bool> NoBuildOption = new ForwardedOption<bool>("--no-build", LocalizableStrings.NoBuildOptionDescription)
            .ForwardAs("-property:NoBuild=true");

        public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs("-nologo");

        public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public static readonly Option<bool> SelfContainedOption = CommonOptions.SelfContainedOption;

        public static readonly Option<bool> NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

        public static readonly Option<string> RuntimeOption = CommonOptions.RuntimeOption;

        public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

        public static readonly Option<string> ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("publish", DocsLink, LocalizableStrings.AppDescription);

            command.AddArgument(SlnOrProjectArgument);
            RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: false, includeNoDependenciesOption: true);
            command.AddOption(OuputOption);
            command.AddOption(CommonOptions.ArtifactsPathOption);
            command.AddOption(ManifestOption);
            command.AddOption(NoBuildOption);
            command.AddOption(SelfContainedOption);
            command.AddOption(NoSelfContainedOption);
            command.AddOption(NoLogoOption);
            command.AddOption(FrameworkOption);
            command.AddOption(RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.AddOption(ConfigurationOption);
            command.AddOption(CommonOptions.VersionSuffixOption);
            command.AddOption(CommonOptions.InteractiveMsBuildForwardOption);
            command.AddOption(NoRestoreOption);
            command.AddOption(CommonOptions.VerbosityOption);
            command.AddOption(CommonOptions.ArchitectureOption);
            command.AddOption(CommonOptions.OperatingSystemOption);
            command.AddOption(CommonOptions.DisableBuildServersOption);

            command.SetHandler(PublishCommand.Run);

            return command;
        }
    }
}
