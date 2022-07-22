// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Format;
using Microsoft.DotNet.Tools.Help;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.New;
using Microsoft.DotNet.Tools.NuGet;
using Command = System.CommandLine.Command;
using CommandResult = System.CommandLine.Parsing.CommandResult;

namespace Microsoft.DotNet.Cli
{
    public static class Parser
    {
        public static readonly RootCommand RootCommand = new RootCommand();

        internal static Dictionary<Option, Dictionary<Command, string>> HelpDescriptionCustomizations = new Dictionary<Option, Dictionary<Command, string>>();

        public static readonly Command InstallSuccessCommand = InternalReportinstallsuccessCommandParser.GetCommand();

        // Subcommands
        public static readonly Command[] Subcommands = new Command[]
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

        // Options
        public static readonly Option<bool> DiagOption = new Option<bool>(new[] { "-d", "--diagnostics" });

        public static readonly Option<bool> VersionOption = new Option<bool>("--version");

        public static readonly Option<bool> InfoOption = new Option<bool>("--info");

        public static readonly Option<bool> ListSdksOption = new Option<bool>("--list-sdks");

        public static readonly Option<bool> ListRuntimesOption = new Option<bool>("--list-runtimes");

        // Argument
        public static readonly Argument<string> DotnetSubCommand = new Argument<string>() { Arity = ArgumentArity.ExactlyOne, IsHidden = true };

        private static Command ConfigureCommandLine(Command rootCommand)
        {
            // Add subcommands
            foreach (var subcommand in Subcommands)
            {
                rootCommand.AddCommand(subcommand);
            }

            // Add options
            rootCommand.AddOption(DiagOption);
            rootCommand.AddOption(VersionOption);
            rootCommand.AddOption(InfoOption);
            rootCommand.AddOption(ListSdksOption);
            rootCommand.AddOption(ListRuntimesOption);

            // Add argument
            rootCommand.AddArgument(DotnetSubCommand);

            return rootCommand;
        }

        private static CommandLineBuilder DisablePosixBinding(this CommandLineBuilder builder)
        {
            builder.EnablePosixBundling(false);
            return builder;
        }

        public static Command GetBuiltInCommand(string commandName)
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
                        // Remove content in the lines that contain #, starting from the point of the #
                        .Select(line => {
                            var hashPos = line.IndexOf('#');
                            if (hashPos == -1) {
                                return line;
                            } else if (hashPos == 0) {
                                return "";
                            } else {
                                return line.Substring(0, hashPos).Trim();
                            }
                        })
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

        public static System.CommandLine.Parsing.Parser Instance { get; } = new CommandLineBuilder(ConfigureCommandLine(RootCommand))
            .UseExceptionHandler(ExceptionHandler)
            // TODO:CH - we want this for dotnet-new argument reporting, but 
            //           adding this makes forwarding commands (which can't know
            //           all of the parameters of their wrapped command by design)
            //           error. so `dotnet msbuild /t:thing` throws a parse error.
            // .UseParseErrorReporting(127)
            .UseParseErrorReporting("new")
            .UseHelp()
            .UseHelpBuilder(context => DotnetHelpBuilder.Instance.Value)
            .UseLocalizationResources(new CommandLineValidationMessages())
            .UseParseDirective()
            .UseSuggestDirective()
            .DisablePosixBinding()
            .UseTokenReplacer(TokenPerLine)
            .Build();

        private static CommandLineBuilder UseParseErrorReporting(this CommandLineBuilder builder, string commandName)
        {
            builder.AddMiddleware(async (context, next) =>
            {
                CommandResult currentCommandResult = context.ParseResult.CommandResult;
                while (currentCommandResult != null && currentCommandResult.Command.Name != commandName)
                {
                    currentCommandResult = currentCommandResult.Parent as CommandResult;
                }

                if (currentCommandResult == null || !context.ParseResult.Errors.Any())
                {
                    //different command was launched or no errors
                    await next(context).ConfigureAwait(false);
                }
                else 
                {
                    context.ExitCode = 127; //parse error
                    //TODO: discuss to make coloring extensions public
                    //context.Console.ResetTerminalForegroundColor();
                    //context.Console.SetTerminalForegroundRed();
                    foreach (var error in context.ParseResult.Errors)
                    {
                        context.Console.Error.WriteLine(error.Message);
                    }
                    context.Console.Error.WriteLine();
                    //context.Console.ResetTerminalForegroundColor();
                    var output = context.Console.Out.CreateTextWriter();
                    var helpContext = new HelpContext(context.HelpBuilder,
                                                      context.ParseResult.CommandResult.Command,
                                                      output,
                                                      context.ParseResult);
                    context.HelpBuilder
                           .Write(helpContext);
                }
            }, MiddlewareOrder.ErrorReporting);
            return builder;
        }

        private static void ExceptionHandler(Exception exception, InvocationContext context)
        {
            if (exception is TargetInvocationException)
            {
                exception = exception.InnerException;
            }

            if (exception is Utils.GracefulException)
            {
                Reporter.Error.WriteLine(CommandContext.IsVerbose()
                    ? exception.ToString().Red().Bold()
                    : exception.Message.Red().Bold());
            }
            else if (exception is CommandParsingException)
            {
                Reporter.Error.WriteLine(CommandContext.IsVerbose()
                    ? exception.ToString().Red().Bold()
                    : exception.Message.Red().Bold());
                context.ParseResult.ShowHelp();
            }
            else
            {
                Reporter.Error.Write("Unhandled exception: ".Red().Bold());
                Reporter.Error.WriteLine(exception.ToString().Red().Bold());
            }
            context.ExitCode = 1;
        }

        internal class CommandLineConsole : IConsole
        {
            public IStandardStreamWriter Out => StandardStreamWriter.Create(Console.Out);

            public bool IsOutputRedirected => Console.IsOutputRedirected;

            public IStandardStreamWriter Error => StandardStreamWriter.Create(Console.Error);

            public bool IsErrorRedirected => Console.IsErrorRedirected;

            public bool IsInputRedirected => Console.IsInputRedirected;
        }

        internal class DotnetHelpBuilder : HelpBuilder
        {
            private DotnetHelpBuilder(int maxWidth = int.MaxValue) : base(LocalizationResources.Instance, maxWidth) { }

            public static Lazy<HelpBuilder> Instance = new Lazy<HelpBuilder>(() => {
                int windowWidth;
                try
                {
                    windowWidth = Console.WindowWidth;
                }
                catch
                {
                    windowWidth = int.MaxValue;
                }

                DotnetHelpBuilder dotnetHelpBuilder = new DotnetHelpBuilder(windowWidth);

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

            public override void Write(HelpContext context)
            {
                var command = context.Command;
                var helpArgs = new string[] { "--help" };
                if (command.Equals(RootCommand))
                {
                    Console.Out.WriteLine(HelpUsageText.UsageText);
                }
                else if (command.Name.Equals(NuGetCommandParser.GetCommand().Name))
                {
                    NuGetCommand.Run(helpArgs);
                }
                else if (command.Name.Equals(MSBuildCommandParser.GetCommand().Name))
                {
                    new MSBuildForwardingApp(helpArgs).Execute();
                }
                else if (command.Name.Equals(VSTestCommandParser.GetCommand().Name))
                {
                    new VSTestForwardingApp(helpArgs).Execute();
                }
                else if (command.Name.Equals(FormatCommandParser.GetCommand().Name))
                {
                    var argumetns = context.ParseResult.GetValueForArgument(FormatCommandParser.Arguments);
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
                        ListCommandParser.SlnOrProjectArgument.Name = CommonLocalizableStrings.ProjectArgumentName;
                        ListCommandParser.SlnOrProjectArgument.Description = CommonLocalizableStrings.ProjectArgumentDescription;
                    }
                    else if (command.Name.Equals(AddPackageParser.GetCommand().Name) || command.Name.Equals(AddCommandParser.GetCommand().Name))
                    {
                        // Don't show package completions in help
                        AddPackageParser.CmdPackageArgument.Completions.Clear();
                    }

                    base.Write(context);
                }
            }
        }
    }
}
