// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolCommandParser
    {
        public static Command GetCommand()
        {
            var command = new Command("tool", LocalizableStrings.CommandDescription);

            command.AddCommand(ToolInstallCommandParser.GetCommand());
            command.AddCommand(ToolUninstallCommandParser.GetCommand());
            command.AddCommand(ToolUpdateCommandParser.GetCommand());
            command.AddCommand(ToolListCommandParser.GetCommand());
            command.AddCommand(ToolRunCommandParser.GetCommand());
            command.AddCommand(ToolSearchCommandParser.GetCommand());
            command.AddCommand(ToolRestoreCommandParser.GetCommand());

            return command;
        }
    }
}
