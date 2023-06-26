// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Help
{
    internal static class HelpCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-help";

        public static readonly Argument<string> Argument = new Argument<string>(LocalizableStrings.CommandArgumentName)
        {
            Description = LocalizableStrings.CommandArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("help", DocsLink, LocalizableStrings.AppFullName);

            command.AddArgument(Argument);

            command.SetHandler(HelpCommand.Run);

            return command;
        }
    }
}

