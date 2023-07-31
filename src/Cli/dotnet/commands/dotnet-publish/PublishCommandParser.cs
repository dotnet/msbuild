// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Publish;
using LocalizableStrings = Microsoft.DotNet.Tools.Publish.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PublishCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-publish";

        public static readonly CliArgument<IEnumerable<string>> SlnOrProjectArgument = new(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly CliOption<string> OuputOption = new ForwardedOption<string>("--output", "-o")
        {
            Description = LocalizableStrings.OutputOptionDescription,
            HelpName = LocalizableStrings.OutputOption
        }.ForwardAsOutputPath("PublishDir");

        public static readonly CliOption<IEnumerable<string>> ManifestOption = new ForwardedOption<IEnumerable<string>>("--manifest")
        {
            Description = LocalizableStrings.ManifestOptionDescription,
            HelpName = LocalizableStrings.ManifestOption
        }.ForwardAsSingle(o => $"-property:TargetManifestFiles={string.Join("%3B", o.Select(CommandDirectoryContext.GetFullPath))}")
        .AllowSingleArgPerToken();

        public static readonly CliOption<bool> NoBuildOption = new ForwardedOption<bool>("--no-build")
        {
            Description = LocalizableStrings.NoBuildOptionDescription
        }.ForwardAs("-property:NoBuild=true");

        public static readonly CliOption<bool> NoLogoOption = new ForwardedOption<bool>("--nologo")
        {
            Description = LocalizableStrings.CmdNoLogo
        }.ForwardAs("-nologo");

        public static readonly CliOption<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public static readonly CliOption<bool> SelfContainedOption = CommonOptions.SelfContainedOption;

        public static readonly CliOption<bool> NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

        public static readonly CliOption<string> RuntimeOption = CommonOptions.RuntimeOption;

        public static readonly CliOption<string> FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

        public static readonly CliOption<string> ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new DocumentedCommand("publish", DocsLink, LocalizableStrings.AppDescription);

            command.Arguments.Add(SlnOrProjectArgument);
            RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: false, includeNoDependenciesOption: true);

            command.Options.Add(OuputOption);
            command.Options.Add(CommonOptions.ArtifactsPathOption);
            command.Options.Add(ManifestOption);
            command.Options.Add(NoBuildOption);
            command.Options.Add(SelfContainedOption);
            command.Options.Add(NoSelfContainedOption);
            command.Options.Add(NoLogoOption);
            command.Options.Add(FrameworkOption);
            command.Options.Add(RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.Options.Add(ConfigurationOption);
            command.Options.Add(CommonOptions.VersionSuffixOption);
            command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
            command.Options.Add(NoRestoreOption);
            command.Options.Add(CommonOptions.VerbosityOption);
            command.Options.Add(CommonOptions.ArchitectureOption);
            command.Options.Add(CommonOptions.OperatingSystemOption);
            command.Options.Add(CommonOptions.DisableBuildServersOption);

            command.SetAction(PublishCommand.Run);

            return command;
        }
    }
}
