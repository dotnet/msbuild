// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.List.PackageReferences;
using LocalizableStrings = Microsoft.DotNet.Tools.List.PackageReferences.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListPackageReferencesCommandParser
    {
        public static readonly CliOption OutdatedOption = new ForwardedOption<bool>("--outdated")
        {
            Description = LocalizableStrings.CmdOutdatedDescription
        }.ForwardAs("--outdated");

        public static readonly CliOption DeprecatedOption = new ForwardedOption<bool>("--deprecated")
        {
            Description = LocalizableStrings.CmdDeprecatedDescription
        }.ForwardAs("--deprecated");

        public static readonly CliOption VulnerableOption = new ForwardedOption<bool>("--vulnerable")
        {
            Description = LocalizableStrings.CmdVulnerableDescription
        }.ForwardAs("--vulnerable");

        public static readonly CliOption FrameworkOption = new ForwardedOption<IEnumerable<string>>("--framework")
        {
            Description = LocalizableStrings.CmdFrameworkDescription,
            HelpName = LocalizableStrings.CmdFramework
        }.ForwardAsManyArgumentsEachPrefixedByOption("--framework")
        .AllowSingleArgPerToken();

        public static readonly CliOption TransitiveOption = new ForwardedOption<bool>("--include-transitive")
        {
            Description = LocalizableStrings.CmdTransitiveDescription
        }.ForwardAs("--include-transitive");

        public static readonly CliOption PrereleaseOption = new ForwardedOption<bool>("--include-prerelease")
        {
            Description = LocalizableStrings.CmdPrereleaseDescription
        }.ForwardAs("--include-prerelease");

        public static readonly CliOption HighestPatchOption = new ForwardedOption<bool>("--highest-patch")
        {
            Description = LocalizableStrings.CmdHighestPatchDescription
        }.ForwardAs("--highest-patch");

        public static readonly CliOption HighestMinorOption = new ForwardedOption<bool>("--highest-minor")
        {
            Description = LocalizableStrings.CmdHighestMinorDescription
        }.ForwardAs("--highest-minor");

        public static readonly CliOption ConfigOption = new ForwardedOption<string>("--config", "--configfile")
        {
            Description = LocalizableStrings.CmdConfigDescription,
            HelpName = LocalizableStrings.CmdConfig
        }.ForwardAsMany(o => new[] { "--config", o });

        public static readonly CliOption SourceOption = new ForwardedOption<IEnumerable<string>>("--source")
        {
            Description = LocalizableStrings.CmdSourceDescription,
            HelpName = LocalizableStrings.CmdSource
        }.ForwardAsManyArgumentsEachPrefixedByOption("--source")
        .AllowSingleArgPerToken();

        public static readonly CliOption InteractiveOption = new ForwardedOption<bool>("--interactive")
        {
            Description = CommonLocalizableStrings.CommandInteractiveOptionDescription
        }.ForwardAs("--interactive");

        public static readonly CliOption VerbosityOption = new ForwardedOption<VerbosityOptions>("--verbosity", "-v")
        {
            Description = CommonLocalizableStrings.VerbosityOptionDescription,
            HelpName = CommonLocalizableStrings.LevelArgumentName
        }.ForwardAsSingle(o => $"--verbosity:{o}");

        public static readonly CliOption FormatOption = new ForwardedOption<ReportOutputFormat>("--format")
        {
            Description = LocalizableStrings.CmdFormatDescription
        }.ForwardAsSingle(o => $"--format:{o}");

        public static readonly CliOption OutputVersionOption = new ForwardedOption<int>("--output-version")
        {
            Description = LocalizableStrings.CmdOutputVersionDescription
        }.ForwardAsSingle(o => $"--output-version:{o}");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("package", LocalizableStrings.AppFullName);

            command.Options.Add(VerbosityOption);
            command.Options.Add(OutdatedOption);
            command.Options.Add(DeprecatedOption);
            command.Options.Add(VulnerableOption);
            command.Options.Add(FrameworkOption);
            command.Options.Add(TransitiveOption);
            command.Options.Add(PrereleaseOption);
            command.Options.Add(HighestPatchOption);
            command.Options.Add(HighestMinorOption);
            command.Options.Add(ConfigOption);
            command.Options.Add(SourceOption);
            command.Options.Add(InteractiveOption);
            command.Options.Add(FormatOption);
            command.Options.Add(OutputVersionOption);

            command.SetAction((parseResult) => new ListPackageReferencesCommand(parseResult).Execute());

            return command;
        }
    }
}
