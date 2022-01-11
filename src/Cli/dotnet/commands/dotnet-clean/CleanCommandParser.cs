// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Clean.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class CleanCommandParser
    {
        public static readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly Option<string> OutputOption = new ForwardedOption<string>(new string[] { "-o", "--output" }, LocalizableStrings.CmdOutputDirDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdOutputDir
        }.ForwardAsSingle(o => $"-property:OutputPath={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs("-nologo");

        public static Command GetCommand()
        {
            var command = new Command("clean", LocalizableStrings.AppFullName);

            command.AddArgument(SlnOrProjectArgument);
            command.AddOption(CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription));
            command.AddOption(CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription));
            command.AddOption(CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription));
            command.AddOption(CommonOptions.InteractiveMsBuildForwardOption());
            command.AddOption(CommonOptions.VerbosityOption());
            command.AddOption(OutputOption);
            command.AddOption(NoLogoOption);

            return command;
        }
    }
}
