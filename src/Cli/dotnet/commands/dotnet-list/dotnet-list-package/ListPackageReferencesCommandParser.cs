// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.List.PackageReferences.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListPackageReferencesCommandParser
    {
        public static readonly Option OutdatedOption = new ForwardedOption<bool>("--outdated", LocalizableStrings.CmdOutdatedDescription)
            .ForwardAs("--outdated");

        public static readonly Option DepreciatedOption = new ForwardedOption<bool>("--deprecated", LocalizableStrings.CmdDeprecatedDescription)
            .ForwardAs("--deprecated");

        public static readonly Option VulnerableOption = new ForwardedOption<bool>("--vulnerable", LocalizableStrings.CmdVulnerableDescription)
            .ForwardAs("--vulnerable");

        public static readonly Option FrameworkOption = new ForwardedOption<IEnumerable<string>>("--framework", LocalizableStrings.CmdFrameworkDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdFramework
        }.ForwardAsManyArgumentsEachPrefixedByOption("--framework")
        .AllowSingleArgPerToken();

        public static readonly Option TransitiveOption = new ForwardedOption<bool>("--include-transitive", LocalizableStrings.CmdTransitiveDescription)
            .ForwardAs("--include-transitive");

        public static readonly Option PrereleaseOption = new ForwardedOption<bool>("--include-prerelease", LocalizableStrings.CmdPrereleaseDescription)
            .ForwardAs("--include-prerelease");

        public static readonly Option HighestPatchOption = new ForwardedOption<bool>("--highest-patch", LocalizableStrings.CmdHighestPatchDescription)
            .ForwardAs("--highest-patch");

        public static readonly Option HighestMinorOption = new ForwardedOption<bool>("--highest-minor", LocalizableStrings.CmdHighestMinorDescription)
            .ForwardAs("--highest-minor");

        public static readonly Option ConfigOption = new ForwardedOption<string>("--config", LocalizableStrings.CmdConfigDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdConfig
        }.ForwardAsMany(o => new[] { "--config", o });

        public static readonly Option SourceOption = new ForwardedOption<IEnumerable<string>>("--source", LocalizableStrings.CmdSourceDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdSource
        }.ForwardAsManyArgumentsEachPrefixedByOption("--source")
        .AllowSingleArgPerToken();

        public static readonly Option InteractiveOption = new ForwardedOption<bool>("--interactive", CommonLocalizableStrings.CommandInteractiveOptionDescription)
            .ForwardAs("--interactive");

        public static Command GetCommand()
        {
            var command = new Command("package", LocalizableStrings.AppFullName);

            command.AddOption(CommonOptions.VerbosityOption(o => $"--verbosity:{o}"));
            command.AddOption(OutdatedOption);
            command.AddOption(DepreciatedOption);
            command.AddOption(VulnerableOption);
            command.AddOption(FrameworkOption);
            command.AddOption(TransitiveOption);
            command.AddOption(PrereleaseOption);
            command.AddOption(HighestPatchOption);
            command.AddOption(HighestMinorOption);
            command.AddOption(ConfigOption);
            command.AddOption(SourceOption);
            command.AddOption(InteractiveOption);

            return command;
        }
    }
}
