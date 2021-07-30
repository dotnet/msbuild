// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Build.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class BuildCommandParser
    {
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

        public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption();

        public static readonly Option SelfContainedOption = CommonOptions.SelfContainedOption();

        public static readonly Option NoSelfContainedOption = CommonOptions.NoSelfContainedOption();

        public static readonly Option RuntimeOption = CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription);

        public static Command GetCommand()
        {
            var command = new Command("build", LocalizableStrings.AppFullName);

            command.AddArgument(SlnOrProjectArgument);
            RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: false, includeNoDependenciesOption: false);
            command.AddOption(CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription));
            command.AddOption(CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription));
            command.AddOption(RuntimeOption);
            command.AddOption(CommonOptions.VersionSuffixOption());
            command.AddOption(NoRestoreOption);
            command.AddOption(CommonOptions.InteractiveMsBuildForwardOption());
            command.AddOption(CommonOptions.VerbosityOption());
            command.AddOption(CommonOptions.DebugOption());
            command.AddOption(OutputOption);
            command.AddOption(NoIncrementalOption);
            command.AddOption(NoDependenciesOption);
            command.AddOption(NoLogoOption);
            command.AddOption(SelfContainedOption);
            command.AddOption(NoSelfContainedOption);
            command.AddOption(CommonOptions.ArchitectureOption());
            command.AddOption(CommonOptions.OperatingSystemOption());

            return command;
        }
    }
}
