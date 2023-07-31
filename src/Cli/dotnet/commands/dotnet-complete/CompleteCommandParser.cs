// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class CompleteCommandParser
    {
        public static readonly CliArgument<string> PathArgument = new("path");

        public static readonly CliOption<int?> PositionOption = new("--position")
        {
            HelpName = "command"
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("complete")
            {
                Hidden = true
            };

            command.Arguments.Add(PathArgument);
            command.Options.Add(PositionOption);

            command.SetAction(CompleteCommand.Run);

            return command;
        }
    }
}
