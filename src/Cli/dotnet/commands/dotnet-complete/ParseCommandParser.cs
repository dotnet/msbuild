// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    internal static class ParseCommandParser
    {
        public static Command GetCommand()
        {
            var command = new Command("parse")
            {
                IsHidden = true
            };

            command.Handler = CommandHandler.Create<ParseResult>(ParseCommand.Run);

            return command;
        }
    }
}
