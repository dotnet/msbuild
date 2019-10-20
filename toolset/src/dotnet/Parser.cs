// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Help;
using Microsoft.DotNet.Tools.New;
using static System.Environment;
using static Microsoft.DotNet.Cli.CommandLine.LocalizableStrings;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;
using NewCommandParser = Microsoft.TemplateEngine.Cli.CommandParsing.CommandParserSupport;

namespace Microsoft.DotNet.Cli
{
    public static class Parser
    {
        // This is used for descriptions of commands and options that are only defined for `dotnet complete` (i.e. command line completion).
        // For example, a NuGet assembly handles parsing the `nuget` command and options.
        // To get completion for such a command, we have to define a parser that is used for the completion.
        // Command and option help text cannot be empty, otherwise the parser will hide them from the completion list.
        // The value of `-` has no special meaning; it simply prevents these commands and options from being hidden.
        internal const string CompletionOnlyDescription = "-";

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
                                    NewCommandParser.CreateNewCommandWithoutTemplateInfo(NewCommandShim.CommandName),
                                    RestoreCommandParser.Restore(),
                                    BuildCommandParser.Build(),
                                    PublishCommandParser.Publish(),
                                    RunCommandParser.Run(),
                                    TestCommandParser.Test(),
                                    PackCommandParser.Pack(),
                                    CleanCommandParser.Clean(),
                                    SlnCommandParser.Sln(),
                                    AddCommandParser.Add(),
                                    RemoveCommandParser.Remove(),
                                    ListCommandParser.List(),
                                    NuGetCommandParser.NuGet(),
                                    StoreCommandParser.Store(),
                                    HelpCommandParser.Help(),
                                    Create.Command("fsi", CompletionOnlyDescription),
                                    Create.Command("msbuild", CompletionOnlyDescription),
                                    Create.Command("vstest", CompletionOnlyDescription),
                                    CompleteCommandParser.Complete(),
                                    InternalReportinstallsuccessCommandParser.InternalReportinstallsuccess(),
                                    ToolCommandParser.Tool(),
                                    BuildServerCommandParser.CreateCommand(),
                                    CommonOptions.HelpOption(),
                                    Create.Option("--info", CompletionOnlyDescription),
                                    Create.Option("-d|--diagnostics", CompletionOnlyDescription),
                                    Create.Option("--version", CompletionOnlyDescription),
                                    Create.Option("--list-sdks", CompletionOnlyDescription),
                                    Create.Option("--list-runtimes", CompletionOnlyDescription)));
    }
}
