// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-list";

        public static readonly Argument<string> SlnOrProjectArgument = new Argument<string>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne
        }.DefaultToCurrentDirectory();

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("list", DocsLink, LocalizableStrings.NetListCommand);

            command.AddArgument(SlnOrProjectArgument);
            command.AddCommand(ListPackageReferencesCommandParser.GetCommand());
            command.AddCommand(ListProjectToProjectReferencesCommandParser.GetCommand());

            command.SetHandler((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
