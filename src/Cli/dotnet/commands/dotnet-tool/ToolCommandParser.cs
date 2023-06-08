// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-tool";

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new("tool", DocsLink, LocalizableStrings.CommandDescription);

            command.Subcommands.Add(ToolInstallCommandParser.GetCommand());
            command.Subcommands.Add(ToolUninstallCommandParser.GetCommand());
            command.Subcommands.Add(ToolUpdateCommandParser.GetCommand());
            command.Subcommands.Add(ToolListCommandParser.GetCommand());
            command.Subcommands.Add(ToolRunCommandParser.GetCommand());
            command.Subcommands.Add(ToolSearchCommandParser.GetCommand());
            command.Subcommands.Add(ToolRestoreCommandParser.GetCommand());

            command.SetAction((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
