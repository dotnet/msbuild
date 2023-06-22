// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
