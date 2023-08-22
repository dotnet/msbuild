// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Run;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolRunCommandParser
    {
        public static readonly CliArgument<string> CommandNameArgument = new("commandName")
        {
            HelpName = LocalizableStrings.CommandNameArgumentName,
            Description = LocalizableStrings.CommandNameArgumentDescription
        };

        public static readonly CliArgument<IEnumerable<string>> CommandArgument = new("toolArguments")
        {
            Description = "arguments forwarded to the tool"
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("run", LocalizableStrings.CommandDescription);

            command.Arguments.Add(CommandNameArgument);
            command.Arguments.Add(CommandArgument);

            command.SetAction((parseResult) => new ToolRunCommand(parseResult).Execute());

            return command;
        }
    }
}
