// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Clean;
using LocalizableStrings = Microsoft.DotNet.Tools.Clean.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class CleanCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-clean";

        public static readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly Option<string> OutputOption = new ForwardedOption<string>(new string[] { "-o", "--output" }, LocalizableStrings.CmdOutputDirDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdOutputDir
        }.ForwardAsOutputPath("OutputPath");

        public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs("-nologo");

        public static readonly Option FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

        public static readonly Option ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("clean", DocsLink, LocalizableStrings.AppFullName);

            command.AddArgument(SlnOrProjectArgument);
            command.AddOption(FrameworkOption);
            command.AddOption(CommonOptions.RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.AddOption(ConfigurationOption);
            command.AddOption(CommonOptions.InteractiveMsBuildForwardOption);
            command.AddOption(CommonOptions.VerbosityOption);
            command.AddOption(OutputOption);
            command.AddOption(CommonOptions.ArtifactsPathOption);
            command.AddOption(NoLogoOption);
            command.AddOption(CommonOptions.DisableBuildServersOption);

            command.SetHandler(CleanCommand.Run);

            return command;
        }
    }
}
