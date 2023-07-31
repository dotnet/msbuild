// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Dotnet_new3
{
    internal static class ParserFactory
    {
        internal static CliConfiguration CreateParser(CliCommand command, bool disableHelp = false)
        {
            CliConfiguration config = new(command)
            //.UseExceptionHandler(ExceptionHandler)
            //.UseLocalizationResources(new CommandLineValidationMessages())
            //TODO: decide if it's needed to implement it; and implement if needed
            //.UseParseDirective()
            //.UseSuggestDirective()
            {
                EnableParseErrorReporting = true, //TODO: discuss with SDK if it is possible to use it.
                EnablePosixBundling = false
            };

            for (int i = 0; i < command.Options.Count; i++)
            {
                if (command.Options[i] is HelpOption)
                {
                    if (disableHelp)
                    {
                        command.Options.RemoveAt(i);
                        return config;
                    }

                    command.Options[i] = CreateCustomHelp();
                    return config;
                }
            }

            if (!disableHelp)
            {
                command.Options.Add(CreateCustomHelp());
            }

            return config;

            static HelpOption CreateCustomHelp()
            {
                HelpOption helpOption = new HelpOption();
                HelpAction helpAction = (HelpAction)helpOption.Action!;
                helpAction.Builder.CustomizeLayout(CustomHelpLayout);
                return helpOption;
            }
        }

        private static IEnumerable<Action<HelpContext>> CustomHelpLayout(HelpContext context)
        {
            if (context.ParseResult.CommandResult.Command is ICustomHelp custom)
            {
                return custom.CustomHelpLayout();
            }
            else
            {
                return HelpBuilder.Default.GetLayout();
            }
        }
    }
}
