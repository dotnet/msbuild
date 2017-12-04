// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Help;
using static System.Environment;
using static Microsoft.DotNet.Cli.CommandLine.LocalizableStrings;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class Parser
    {
        static Parser()
        {
            ConfigureCommandLineLocalizedStrings();
        }

        private static void ConfigureCommandLineLocalizedStrings()
        {
            DefaultHelpViewText.AdditionalArgumentsSection =
                $"{UsageCommandsAdditionalArgsHeader}{NewLine}  {LocalizableStrings.RunCommandAdditionalArgsHelpText}";
            DefaultHelpViewText.ArgumentsSection.Title = UsageArgumentsHeader;
            DefaultHelpViewText.CommandsSection.Title = UsageCommandsHeader;
            DefaultHelpViewText.OptionsSection.Title = UsageOptionsHeader;
            DefaultHelpViewText.Synopsis.AdditionalArguments = UsageCommandAdditionalArgs;
            DefaultHelpViewText.Synopsis.Command = UsageCommandToken;
            DefaultHelpViewText.Synopsis.Options = UsageOptionsToken;
            DefaultHelpViewText.Synopsis.Title = UsageHeader;

            ValidationMessages.Current = new CommandLineValidationMessages();
        }

        public static CommandLine.Parser Instance { get; } = new CommandLine.Parser(
            options: Create.Command("dotnet",
                                    ".NET Command Line Tools",
                                    Accept.NoArguments(),
                                    NewCommandParser.New(),
                                    RestoreCommandParser.Restore(),
                                    BuildCommandParser.Build(),
                                    PublishCommandParser.Publish(),
                                    RunCommandParser.Run(),
                                    TestCommandParser.Test(),
                                    PackCommandParser.Pack(),
                                    MigrateCommandParser.Migrate(),
                                    CleanCommandParser.Clean(),
                                    SlnCommandParser.Sln(),
                                    AddCommandParser.Add(),
                                    RemoveCommandParser.Remove(),
                                    ListCommandParser.List(),
                                    NuGetCommandParser.NuGet(),
                                    StoreCommandParser.Store(),
                                    HelpCommandParser.Help(),
                                    Create.Command("msbuild", ""),
                                    Create.Command("vstest", ""),
                                    CompleteCommandParser.Complete(),
                                    InternalReportinstallsuccessCommandParser.InternalReportinstallsuccess(),
                                    InstallCommandParser.Install(),
                                    CommonOptions.HelpOption(),
                                    Create.Option("--info", ""),
                                    Create.Option("-d", ""),
                                    Create.Option("--debug", "")));
    }
}
