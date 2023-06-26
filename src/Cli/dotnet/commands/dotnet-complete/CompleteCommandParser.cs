// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    internal static class CompleteCommandParser
    {
        public static readonly Argument<string> PathArgument = new Argument<string>("path");

        public static readonly Option<int?> PositionOption = new Option<int?>("--position")
        {
            ArgumentHelpName = "command"
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("complete")
            {
                IsHidden = true
            };

            command.AddArgument(PathArgument);
            command.AddOption(PositionOption);

            command.SetHandler(CompleteCommand.Run);

            return command;
        }
    }
}
