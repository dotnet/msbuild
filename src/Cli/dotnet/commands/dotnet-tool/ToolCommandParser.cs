// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
