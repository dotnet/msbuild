// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Reflection;

using Microsoft.DotNet.Cli.Format;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Help;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.New;
using Microsoft.DotNet.Tools.NuGet;

using Command = System.CommandLine.Command;
using ICommand = System.CommandLine.ICommand;

namespace Microsoft.DotNet.Cli
{
    public static class Parser
    {
        public static readonly RootCommand RootCommand = new RootCommand();

        // Subcommands
        private static readonly Command[] Subcommands = new Command[]
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
            SdkCommandParser.GetCommand()
        };

        // Internal commands
        public static readonly Command InstallSuccessCommand = InternalReportinstallsuccessCommandParser.GetCommand();

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

            rootCommand.AddCommand(WorkloadCommandParser.GetCommand());

            //Add internal commands
            rootCommand.AddCommand(InstallSuccessCommand);

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
            builder.EnablePosixBundling = false;
            return builder;
        }

        public static System.CommandLine.Parsing.Parser Instance { get; } = new CommandLineBuilder(ConfigureCommandLine(RootCommand))
            .UseExceptionHandler(ExceptionHandler)
            .UseHelp()
            .UseHelpBuilder(context => new DotnetHelpBuilder(context.Console))
            .UseResources(new CommandLineValidationMessages())
            .UseParseDirective()
            .UseSuggestDirective()
            .DisablePosixBinding()
            .Build();

        private static void ExceptionHandler(Exception exception, InvocationContext context)
        {
            if (exception is TargetInvocationException)
            {
                exception = exception.InnerException;
            }

            if (exception is Utils.GracefulException)
            {
                context.Console.Error.WriteLine(exception.Message);
            }
            else if (exception is CommandParsingException)
            {
                context.Console.Error.WriteLine(exception.Message);
            }
            else
            {
                context.Console.Error.Write("Unhandled exception: ");
                context.Console.Error.WriteLine(exception.ToString());
            }
            context.ParseResult.ShowHelp();
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
            public DotnetHelpBuilder(IConsole console, int maxWidth = int.MaxValue) : base(console, Resources.Instance, maxWidth) { }

            public static Lazy<HelpBuilder> Instance = new Lazy<HelpBuilder>(() => {
                int windowWidth;
                try
                {
                    windowWidth = System.Console.WindowWidth;
                }
                catch
                {
                    windowWidth = int.MaxValue;
                }

                DotnetHelpBuilder dotnetHelpBuilder = new DotnetHelpBuilder(new SystemConsole(), windowWidth);
                dotnetHelpBuilder.Customize(FormatCommandCommon.DiagnosticsOption, defaultValue: Tools.Format.LocalizableStrings.whichever_ids_are_listed_in_the_editorconfig_file);
                dotnetHelpBuilder.Customize(FormatCommandCommon.IncludeOption, defaultValue: Tools.Format.LocalizableStrings.all_files_in_the_solution_or_project);
                dotnetHelpBuilder.Customize(FormatCommandCommon.ExcludeOption, defaultValue: Tools.Format.LocalizableStrings.none);
                return dotnetHelpBuilder;
            });

            public override void Write(ICommand command)
            {
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
                else if (command.Name.Equals(NewCommandParser.GetCommand().Name))
                {
                    NewCommandShim.Run(helpArgs);
                }
                else if (command.Name.Equals(VSTestCommandParser.GetCommand().Name))
                {
                    new VSTestForwardingApp(helpArgs).Execute();
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
                        // Don't show package suggestions in help
                        AddPackageParser.CmdPackageArgument.Suggestions.Clear();
                    }

                    base.Write(command);
                }
            }
        }
    }
}
