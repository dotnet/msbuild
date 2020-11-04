// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Pack.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PackCommandParser
    {
        public static readonly Argument SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly Option OutputOption = new Option<string>(new string[] { "-o", "--output" }, LocalizableStrings.CmdOutputDirDescription)
        {
            Argument = new Argument<string>(LocalizableStrings.CmdOutputDir)
        }.ForwardAsSingle(o => $"-property:PackageOutputPath={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option NoBuildOption = new Option<bool>("--no-build", LocalizableStrings.CmdNoBuildOptionDescription)
            .ForwardAs("-property:NoBuild=true");

        public static readonly Option IncludeSymbolsOption = new Option<bool>("--include-symbols", LocalizableStrings.CmdIncludeSymbolsDescription)
            .ForwardAs("-property:IncludeSymbols=true");

        public static readonly Option IncludeSourceOption = new Option<bool>("--include-source", LocalizableStrings.CmdIncludeSourceDescription)
            .ForwardAs("-property:IncludeSource=true");

        public static readonly Option ServiceableOption = new Option<bool>(new string[] { "-s", "--serviceable" }, LocalizableStrings.CmdServiceableDescription)
            .ForwardAs("-property:Serviceable=true");

        public static readonly Option NoLogoOption = new Option<bool>("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs("-nologo");

        public static readonly Option NoRestoreOption = CommonOptions.NoRestoreOption();

        public static Command GetCommand()
        {
            var command = new Command("pack", LocalizableStrings.AppFullName);

            command.AddArgument(SlnOrProjectArgument);
            command.AddOption(OutputOption);
            command.AddOption(NoBuildOption);
            command.AddOption(IncludeSymbolsOption);
            command.AddOption(IncludeSourceOption);
            command.AddOption(ServiceableOption);
            command.AddOption(NoLogoOption);
            command.AddOption(CommonOptions.InteractiveMsBuildForwardOption());
            command.AddOption(NoRestoreOption);
            command.AddOption(CommonOptions.VerbosityOption());
            command.AddOption(CommonOptions.VersionSuffixOption());
            command.AddOption(CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription));

            return command;
        }
    }
}
