// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-tool";

        public static Command GetCommand()
        {
            var command = new DocumentedCommand("tool", DocsLink, LocalizableStrings.CommandDescription);

            command.AddCommand(ToolInstallCommandParser.GetCommand());
            command.AddCommand(ToolUninstallCommandParser.GetCommand());
            command.AddCommand(ToolUpdateCommandParser.GetCommand());
            command.AddCommand(ToolListCommandParser.GetCommand());
            command.AddCommand(ToolRunCommandParser.GetCommand());
            command.AddCommand(ToolSearchCommandParser.GetCommand());
            command.AddCommand(ToolRestoreCommandParser.GetCommand());

            command.Handler = CommandHandler.Create((Func<int>)(() => throw new Exception("TODO command not found")));

            return command;
        }
    }
}
