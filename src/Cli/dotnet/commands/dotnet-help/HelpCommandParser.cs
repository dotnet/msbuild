// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Help
{
    internal static class HelpCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-help";

        public static readonly CliArgument<string> Argument = new(LocalizableStrings.CommandArgumentName)
        {
            Description = LocalizableStrings.CommandArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new("help", DocsLink, LocalizableStrings.AppFullName);

            command.Arguments.Add(Argument);

            command.SetAction(HelpCommand.Run);

            return command;
        }
    }
}

