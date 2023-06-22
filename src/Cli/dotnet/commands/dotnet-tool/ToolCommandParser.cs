// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-tool";

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("tool", DocsLink, LocalizableStrings.CommandDescription);

            command.AddCommand(ToolInstallCommandParser.GetCommand());
            command.AddCommand(ToolUninstallCommandParser.GetCommand());
            command.AddCommand(ToolUpdateCommandParser.GetCommand());
            command.AddCommand(ToolListCommandParser.GetCommand());
            command.AddCommand(ToolRunCommandParser.GetCommand());
            command.AddCommand(ToolSearchCommandParser.GetCommand());
            command.AddCommand(ToolRestoreCommandParser.GetCommand());

            command.SetHandler((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
