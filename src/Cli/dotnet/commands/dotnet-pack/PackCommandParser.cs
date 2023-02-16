// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Pack;
using LocalizableStrings = Microsoft.DotNet.Tools.Pack.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PackCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-pack";

        public static readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly Option<string> OutputOption = new ForwardedOption<string>(new string[] { "-o", "--output" }, LocalizableStrings.CmdOutputDirDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdOutputDir
        }.ForwardAsSingle(o => $"-property:PackageOutputPath={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option<bool> NoBuildOption = new ForwardedOption<bool>("--no-build", LocalizableStrings.CmdNoBuildOptionDescription)
            .ForwardAs("-property:NoBuild=true");

        public static readonly Option<bool> IncludeSymbolsOption = new ForwardedOption<bool>("--include-symbols", LocalizableStrings.CmdIncludeSymbolsDescription)
            .ForwardAs("-property:IncludeSymbols=true");

        public static readonly Option<bool> IncludeSourceOption = new ForwardedOption<bool>("--include-source", LocalizableStrings.CmdIncludeSourceDescription)
            .ForwardAs("-property:IncludeSource=true");

        public static readonly Option<bool> ServiceableOption = new ForwardedOption<bool>(new string[] { "-s", "--serviceable" }, LocalizableStrings.CmdServiceableDescription)
            .ForwardAs("-property:Serviceable=true");

        public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs("-nologo");

        public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public static readonly Option<string> ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("pack", DocsLink, LocalizableStrings.AppFullName);

            command.AddArgument(SlnOrProjectArgument);
            command.AddOption(OutputOption);
            command.AddOption(NoBuildOption);
            command.AddOption(IncludeSymbolsOption);
            command.AddOption(IncludeSourceOption);
            command.AddOption(ServiceableOption);
            command.AddOption(NoLogoOption);
            command.AddOption(CommonOptions.InteractiveMsBuildForwardOption);
            command.AddOption(NoRestoreOption);
            command.AddOption(CommonOptions.VerbosityOption);
            command.AddOption(CommonOptions.VersionSuffixOption);
            command.AddOption(ConfigurationOption);
            RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: true, includeNoDependenciesOption: true);

            command.SetHandler(PackCommand.Run);

            return command;
        }
    }
}
