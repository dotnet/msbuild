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

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("internal-reportinstallsuccess")
            {
                IsHidden = true
            };

            command.AddArgument(Argument);

            command.SetHandler(InternalReportinstallsuccess.Run);

            return command;
        }
    }
}
