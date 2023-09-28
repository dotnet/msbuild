// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Pack;
using LocalizableStrings = Microsoft.DotNet.Tools.Pack.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PackCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-pack";

        public static readonly CliArgument<IEnumerable<string>> SlnOrProjectArgument = new(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly CliOption<string> OutputOption = new ForwardedOption<string>("--output", "-o")
        {
            Description = LocalizableStrings.CmdOutputDirDescription,
            HelpName = LocalizableStrings.CmdOutputDir
        }.ForwardAsSingle(o => $"-property:PackageOutputPath={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly CliOption<bool> NoBuildOption = new ForwardedOption<bool>("--no-build")
        {
            Description = LocalizableStrings.CmdNoBuildOptionDescription
        }.ForwardAs("-property:NoBuild=true");

        public static readonly CliOption<bool> IncludeSymbolsOption = new ForwardedOption<bool>("--include-symbols")
        {
            Description = LocalizableStrings.CmdIncludeSymbolsDescription
        }.ForwardAs("-property:IncludeSymbols=true");

        public static readonly CliOption<bool> IncludeSourceOption = new ForwardedOption<bool>("--include-source")
        {
            Description = LocalizableStrings.CmdIncludeSourceDescription
        }.ForwardAs("-property:IncludeSource=true");

        public static readonly CliOption<bool> ServiceableOption = new ForwardedOption<bool>("--serviceable", "-s")
        {
            Description = LocalizableStrings.CmdServiceableDescription
        }.ForwardAs("-property:Serviceable=true");

        public static readonly CliOption<bool> NoLogoOption = new ForwardedOption<bool>("--nologo")
        {
            Description = LocalizableStrings.CmdNoLogo
        }.ForwardAs("-nologo");

        public static readonly CliOption<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public static readonly CliOption<string> ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new DocumentedCommand("pack", DocsLink, LocalizableStrings.AppFullName);

            command.Arguments.Add(SlnOrProjectArgument);
            command.Options.Add(OutputOption);
            command.Options.Add(CommonOptions.ArtifactsPathOption);
            command.Options.Add(NoBuildOption);
            command.Options.Add(IncludeSymbolsOption);
            command.Options.Add(IncludeSourceOption);
            command.Options.Add(ServiceableOption);
            command.Options.Add(NoLogoOption);
            command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
            command.Options.Add(NoRestoreOption);
            command.Options.Add(CommonOptions.VerbosityOption);
            command.Options.Add(CommonOptions.VersionSuffixOption);
            command.Options.Add(ConfigurationOption);
            command.Options.Add(CommonOptions.DisableBuildServersOption);
            RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: true, includeNoDependenciesOption: true);

            command.SetAction(PackCommand.Run);

            return command;
        }
    }
}
