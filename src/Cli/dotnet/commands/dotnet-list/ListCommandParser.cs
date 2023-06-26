// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
