// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class SlnCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-sln";

        public static readonly Argument<string> SlnArgument = new Argument<string>(LocalizableStrings.SolutionArgumentName)
        {
            Description = LocalizableStrings.SolutionArgumentDescription,
            Arity = ArgumentArity.ExactlyOne
        }.DefaultToCurrentDirectory();

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("sln", DocsLink, LocalizableStrings.AppFullName);

            command.AddArgument(SlnArgument);
            command.AddCommand(SlnAddParser.GetCommand());
            command.AddCommand(SlnListParser.GetCommand());
            command.AddCommand(SlnRemoveParser.GetCommand());

            command.SetHandler((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
