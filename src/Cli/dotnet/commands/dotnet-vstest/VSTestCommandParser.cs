// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.VSTest;

namespace Microsoft.DotNet.Cli
{
    internal static class VSTestCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-vstest";

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("vstest", DocsLink);

            command.AddOption(CommonOptions.TestPlatformOption);
            command.AddOption(CommonOptions.TestFrameworkOption);
            command.AddOption(CommonOptions.TestLoggerOption);

            command.SetHandler(VSTestCommand.Run);

            return command;
        }
    }
}
