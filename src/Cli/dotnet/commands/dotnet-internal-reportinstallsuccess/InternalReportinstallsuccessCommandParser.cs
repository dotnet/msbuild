// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    internal static class InternalReportinstallsuccessCommandParser
    {
        public static readonly Argument<string> Argument = new Argument<string>("internal-reportinstallsuccess-arg");

        public static Command GetCommand()
        {
            var command = new Command("internal-reportinstallsuccess")
            {
                IsHidden = true
            };

            command.AddArgument(Argument);

            command.Handler = CommandHandler.Create<ParseResult>(InternalReportinstallsuccess.Run);

            return command;
        }
    }
}
