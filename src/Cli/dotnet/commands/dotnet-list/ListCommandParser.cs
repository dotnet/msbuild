// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-list";

        public static readonly CliArgument<string> SlnOrProjectArgument = CreateSlnOrProjectArgument(CommonLocalizableStrings.SolutionOrProjectArgumentName, CommonLocalizableStrings.SolutionOrProjectArgumentDescription);

        internal static CliArgument<string> CreateSlnOrProjectArgument(string name, string description)
            => new CliArgument<string>(name)
            {
                Description = description,
                Arity = ArgumentArity.ZeroOrOne
            }.DefaultToCurrentDirectory();

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new DocumentedCommand("list", DocsLink, LocalizableStrings.NetListCommand);

            command.Arguments.Add(SlnOrProjectArgument);
            command.Subcommands.Add(ListPackageReferencesCommandParser.GetCommand());
            command.Subcommands.Add(ListProjectToProjectReferencesCommandParser.GetCommand());

            command.SetAction((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
