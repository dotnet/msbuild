// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Build;
using LocalizableStrings = Microsoft.DotNet.Tools.Build.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class BuildCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-build";

        public static readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly Option<string> OutputOption = new ForwardedOption<string>(new string[] { "-o", "--output" }, LocalizableStrings.OutputOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.OutputOptionName
        }.ForwardAsSingle(arg => $"-property:OutputPath={CommandDirectoryContext.GetFullPath(arg)}");

        public static readonly Option<bool> NoIncrementalOption = new Option<bool>("--no-incremental", LocalizableStrings.NoIncrementalOptionDescription);

        public static readonly Option<bool> NoDependenciesOption = new ForwardedOption<bool>("--no-dependencies", LocalizableStrings.NoDependenciesOptionDescription)
            .ForwardAs("-property:BuildProjectReferences=false");

        public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs("-nologo");

        public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public static readonly Option SelfContainedOption = CommonOptions.SelfContainedOption;

        public static readonly Option NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

        public static readonly Option RuntimeOption = CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription);

        public static readonly Option FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

        public static readonly Option ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("build", DocsLink, LocalizableStrings.AppFullName);

            command.AddArgument(SlnOrProjectArgument);
            RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: false, includeNoDependenciesOption: false);
            command.AddOption(FrameworkOption);
            command.AddOption(ConfigurationOption);
            command.AddOption(RuntimeOption);
            command.AddOption(CommonOptions.VersionSuffixOption);
            command.AddOption(NoRestoreOption);
            command.AddOption(CommonOptions.InteractiveMsBuildForwardOption);
            command.AddOption(CommonOptions.VerbosityOption);
            command.AddOption(CommonOptions.DebugOption);
            command.AddOption(OutputOption);
            command.AddOption(NoIncrementalOption);
            command.AddOption(NoDependenciesOption);
            command.AddOption(NoLogoOption);
            command.AddOption(SelfContainedOption);
            command.AddOption(NoSelfContainedOption);
            command.AddOption(CommonOptions.ArchitectureOption());
            command.AddOption(CommonOptions.OperatingSystemOption);

            command.Handler = CommandHandler.Create<ParseResult>(BuildCommand.Run);

            return command;
        }
    }
}
