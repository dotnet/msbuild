// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Help;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Format;
using Microsoft.DotNet.Tools.Help;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.NuGet;
using Microsoft.TemplateEngine.Cli;

namespace Microsoft.DotNet.Cli
{
    public static class Parser
    {
        public static readonly CliRootCommand RootCommand = new();

        internal static Dictionary<CliOption, Dictionary<CliCommand, string>> HelpDescriptionCustomizations = new();

        public static readonly CliCommand InstallSuccessCommand = InternalReportinstallsuccessCommandParser.GetCommand();

        // Subcommands
        public static readonly CliCommand[] Subcommands = new CliCommand[]
        {
            AddCommandParser.GetCommand(),
            BuildCommandParser.GetCommand(),
            BuildServerCommandParser.GetCommand(),
            CleanCommandParser.GetCommand(),
            FormatCommandParser.GetCommand(),
            CompleteCommandParser.GetCommand(),
            FsiCommandParser.GetCommand(),
            ListCommandParser.GetCommand(),
            MSBuildCommandParser.GetCommand(),
            NewCommandParser.GetCommand(),
            NuGetCommandParser.GetCommand(),
            PackCommandParser.GetCommand(),
            ParseCommandParser.GetCommand(),
            PublishCommandParser.GetCommand(),
            RemoveCommandParser.GetCommand(),
            RestoreCommandParser.GetCommand(),
            RunCommandParser.GetCommand(),
            SlnCommandParser.GetCommand(),
            StoreCommandParser.GetCommand(),
            TestCommandParser.GetCommand(),
            ToolCommandParser.GetCommand(),
            VSTestCommandParser.GetCommand(),
            HelpCommandParser.GetCommand(),
            SdkCommandParser.GetCommand(),
            InstallSuccessCommand,
            WorkloadCommandParser.GetCommand()
        };

        public static readonly CliOption<bool> DiagOption = CommonOptionsFactory.CreateDiagnosticsOption(recursive: false);

        public static readonly CliOption<bool> VersionOption = new("--version");

        public static readonly CliOption<bool> InfoOption = new("--info");

        public static readonly CliOption<bool> ListSdksOption = new("--list-sdks");

        public static readonly CliOption<bool> ListRuntimesOption = new("--list-runtimes");

        // Argument
        public static readonly CliArgument<string> DotnetSubCommand = new("subcommand") { Arity = ArgumentArity.ZeroOrOne, Hidden = true };

        private static CliCommand ConfigureCommandLine(CliCommand rootCommand)
        {
            for (int i = rootCommand.Options.Count - 1; i >= 0; i--)
            {
                CliOption option = rootCommand.Options[i];

                if (option is VersionOption)
                {
                    rootCommand.Options.RemoveAt(i);
                }
                else if (option is HelpOption helpOption)
                {
                    helpOption.Action = new HelpAction()
                    {
                        Builder = DotnetHelpBuilder.Instance.Value
                    };

                    option.Description = CommandLineValidation.LocalizableStrings.ShowHelpInfo;
                }
            }

            // Add subcommands
            foreach (var subcommand in Subcommands)
            {
                rootCommand.Subcommands.Add(subcommand);
            }

            // Add options
            rootCommand.Options.Add(DiagOption);
            rootCommand.Options.Add(VersionOption);
            rootCommand.Options.Add(InfoOption);
            rootCommand.Options.Add(ListSdksOption);
            rootCommand.Options.Add(ListRuntimesOption);

            // Add argument
            rootCommand.Arguments.Add(DotnetSubCommand);

            rootCommand.SetAction(parseResult =>
            {
                if (parseResult.GetValue(DiagOption) && parseResult.Tokens.Count == 1)
                {
                    // when user does not specify any args except of diagnostics ("dotnet -d"), we do nothing
                    // as Program.ProcessArgs already enabled the diagnostic output
                    return 0;
                }
                else
                {
                    // when user does not specify any args (just "dotnet"), a usage needs to be printed
                    parseResult.Configuration.Output.WriteLine(HelpUsageText.UsageText);
                    return 0;
                }
            });

            return rootCommand;
        }

        public static CliCommand GetBuiltInCommand(string commandName)
        {
            return Subcommands
                .FirstOrDefault(c => c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Implements token-per-line response file handling for the CLI. We use this instead of the built-in S.CL handling
        /// to ensure backwards-compatibility with MSBuild.
        /// </summary>
        public static bool TokenPerLine(string tokenToReplace, out IReadOnlyList<string> replacementTokens, out string errorMessage) {
            var filePath = Path.GetFullPath(tokenToReplace);
            if (File.Exists(filePath)) {
                var lines = File.ReadAllLines(filePath);
                var trimmedLines =
                    lines
                        // Remove content in the lines that start with # after trimmer leading whitespace
                        .Select(line => line.TrimStart().StartsWith('#') ? string.Empty : line)
                        // trim leading/trailing whitespace to not pass along dead spaces
                        .Select(x => x.Trim())
                        // Remove empty lines
                        .Where(line => line.Length > 0);
                replacementTokens = trimmedLines.ToArray();
                errorMessage = null;
                return true;
            } else {
                replacementTokens = null;
                errorMessage = string.Format(CommonLocalizableStrings.ResponseFileNotFound, tokenToReplace);
                return false;
            }
        }

        public static CliConfiguration Instance { get; } = new(ConfigureCommandLine(RootCommand))
        {
            EnableDefaultExceptionHandler = false,
            EnableParseErrorReporting = true,
            EnablePosixBundling = false,
            Directives = { new DiagramDirective(), new SuggestDirective() },
            ResponseFileTokenReplacer = TokenPerLine
        };

        internal static int ExceptionHandler(Exception exception, ParseResult parseResult)
        {
            if (exception is TargetInvocationException)
            {
                exception = exception.InnerException;
            }

            if (exception is Utils.GracefulException)
            {
                Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose
                    ? exception.ToString().Red().Bold()
                    : exception.Message.Red().Bold());
            }
            else if (exception is CommandParsingException)
            {
                Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose
                    ? exception.ToString().Red().Bold()
                    : exception.Message.Red().Bold());
                parseResult.ShowHelp();
            }
            else
            {
                Reporter.Error.Write("Unhandled exception: ".Red().Bold());
                Reporter.Error.WriteLine(exception.ToString().Red().Bold());
            }

            return 1;
        }

        internal class DotnetHelpBuilder : HelpBuilder
        {
            private DotnetHelpBuilder(int maxWidth = int.MaxValue) : base(maxWidth) { }

            public static Lazy<HelpBuilder> Instance = new(() => {
                int windowWidth;
                try
                {
                    windowWidth = Console.WindowWidth;
                }
                catch
                {
                    windowWidth = int.MaxValue;
                }

                DotnetHelpBuilder dotnetHelpBuilder = new(windowWidth);

                SetHelpCustomizations(dotnetHelpBuilder);

                return dotnetHelpBuilder;
            });

            private static void SetHelpCustomizations(HelpBuilder builder)
            {
                foreach (var option in HelpDescriptionCustomizations.Keys)
                {
                    Func<HelpContext, string> descriptionCallback = (HelpContext context) =>
                    {
                        foreach (var (command, helpText) in HelpDescriptionCustomizations[option])
                        {
                            if (context.ParseResult.CommandResult.Command.Equals(command))
                            {
                                return helpText;
                            }
                        }
                        return null;
                    };
                    builder.CustomizeSymbol(option, secondColumnText: descriptionCallback);
                }
            }

            public void additionalOption(HelpContext context)
            {
                List<TwoColumnHelpRow> options = new();
                HashSet<CliOption> uniqueOptions = new();
                foreach (CliOption option in context.Command.Options)
                {
                    if (!option.Hidden && uniqueOptions.Add(option))
                    {
                        options.Add(context.HelpBuilder.GetTwoColumnRow(option, context));
                    }
                }

                if (options.Count <= 0)
                {
                    return;
                }

                context.Output.WriteLine(CommonLocalizableStrings.MSBuildAdditionalOptionTitle);
                context.HelpBuilder.WriteColumns(options, context);
                context.Output.WriteLine();
            }

            public override void Write(HelpContext context)
            {
                var command = context.Command;
                var helpArgs = new string[] { "--help" };
                if (command.Equals(RootCommand))
                {
                    Console.Out.WriteLine(HelpUsageText.UsageText);
                    return;
                }

                foreach (var option in command.Options)
                {
                    option.EnsureHelpName();
                }

                if (command.Equals(NuGetCommandParser.GetCommand()) || command.Parents.Any(parent => parent == NuGetCommandParser.GetCommand()))
                {
                    NuGetCommand.Run(context.ParseResult);
                }
                else if (command.Name.Equals(MSBuildCommandParser.GetCommand().Name))
                {
                    new MSBuildForwardingApp(helpArgs).Execute();
                    context.Output.WriteLine();
                    additionalOption(context);
                }
                else if (command.Name.Equals(VSTestCommandParser.GetCommand().Name))
                {
                    new VSTestForwardingApp(helpArgs).Execute();
                }
                else if (command.Name.Equals(FormatCommandParser.GetCommand().Name))
                {
                    var argumetns = context.ParseResult.GetValue(FormatCommandParser.Arguments);
                    new DotnetFormatForwardingApp(argumetns.Concat(helpArgs).ToArray()).Execute();
                }
                else if (command.Name.Equals(FsiCommandParser.GetCommand().Name))
                {
                    new FsiForwardingApp(helpArgs).Execute();
                }
                else if (command is Microsoft.TemplateEngine.Cli.Commands.ICustomHelp helpCommand)
                {
                    var blocks = helpCommand.CustomHelpLayout();
                    foreach (var block in blocks)
                    {
                        block(context);
                    }
                }
                else if (command.Name.Equals(FormatCommandParser.GetCommand().Name))
                {
                    new DotnetFormatForwardingApp(helpArgs).Execute();
                }
                else if (command.Name.Equals(FsiCommandParser.GetCommand().Name))
                {
                    new FsiForwardingApp(helpArgs).Execute();
                }
                else
                {
                    if (command.Name.Equals(ListProjectToProjectReferencesCommandParser.GetCommand().Name))
                    {
                        CliCommand listCommand = command.Parents.Single() as CliCommand;

                        for (int i = 0; i < listCommand.Arguments.Count; i++)
                        {
                            if (listCommand.Arguments[i].Name == CommonLocalizableStrings.SolutionOrProjectArgumentName)
                            {
                                // Name is immutable now, so we create a new Argument with the right name..
                                listCommand.Arguments[i] = ListCommandParser.CreateSlnOrProjectArgument(CommonLocalizableStrings.ProjectArgumentName, CommonLocalizableStrings.ProjectArgumentDescription);
                            }
                        }
                    }
                    else if (command.Name.Equals(AddPackageParser.GetCommand().Name) || command.Name.Equals(AddCommandParser.GetCommand().Name))
                    {
                        // Don't show package completions in help
                        AddPackageParser.CmdPackageArgument.CompletionSources.Clear();
                    }

                    base.Write(context);
                }
            }
        }
    }
}
