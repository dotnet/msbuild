// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    internal static class SdkCheckCommandParser
    {
        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("check", LocalizableStrings.AppFullName);

            command.SetHandler(SdkCheckCommand.Run);

            return command;
        }
    }
}
