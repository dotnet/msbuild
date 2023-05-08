// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.Sdk.Check;
using LocalizableStrings = Microsoft.DotNet.Tools.Sdk.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class SdkCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-sdk";

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("sdk", DocsLink, LocalizableStrings.AppFullName);
            command.AddCommand(SdkCheckCommandParser.GetCommand());

            command.SetHandler((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
