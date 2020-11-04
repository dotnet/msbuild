// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.List.PackageReferences;
using LocalizableStrings = Microsoft.DotNet.Tools.List.PackageReferences.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListPackageReferencesCommandParser
    {
        public static readonly Option OutdatedOption = new Option("--outdated", LocalizableStrings.CmdOutdatedDescription)
            .ForwardAs("--outdated");

        public static readonly Option DepreciatedOption = new Option("--deprecated", LocalizableStrings.CmdDeprecatedDescription)
            .ForwardAs("--deprecated");

        public static readonly Option VulnerableOption = new Option("--vulnerable", LocalizableStrings.CmdVulnerableDescription)
            .ForwardAs("--vulnerable");

        public static readonly Option FrameworkOption = new Option("--framework", LocalizableStrings.CmdFrameworkDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdFramework) { Arity = ArgumentArity.OneOrMore }
        }.ForwardAsMany<IEnumerable<string>>(o => ForwardedArguments("--framework", o));

        public static readonly Option TransitiveOption = new Option("--include-transitive", LocalizableStrings.CmdTransitiveDescription)
            .ForwardAs("--include-transitive");

        public static readonly Option PrereleaseOption = new Option("--include-prerelease", LocalizableStrings.CmdPrereleaseDescription)
            .ForwardAs("--include-prerelease");

        public static readonly Option HighestPatchOption = new Option("--highest-patch", LocalizableStrings.CmdHighestPatchDescription)
            .ForwardAs("--highest-patch");

        public static readonly Option HighestMinorOption = new Option("--highest-minor", LocalizableStrings.CmdHighestMinorDescription)
            .ForwardAs("--highest-minor");

        public static readonly Option ConfigOption = new Option("--config", LocalizableStrings.CmdConfigDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdConfig) { Arity = ArgumentArity.ExactlyOne }
        }.ForwardAsMany<string>(o => new[] { "--config", o });

        public static readonly Option SourceOption = new Option("--source", LocalizableStrings.CmdSourceDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdSource) { Arity = ArgumentArity.OneOrMore }
        }.ForwardAsMany<IEnumerable<string>>(o => ForwardedArguments("--source", o));

        public static readonly Option InteractiveOption = new Option("--interactive", CommonLocalizableStrings.CommandInteractiveOptionDescription)
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

        public static IEnumerable<string> ForwardedArguments(string token, IEnumerable<string> arguments)
        {
            foreach (var arg in arguments)
            {
                yield return token;
                yield return arg;
            }
        }
    }
}
