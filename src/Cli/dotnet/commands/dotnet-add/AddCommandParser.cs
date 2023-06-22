// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-add";

        public static readonly Argument<string> ProjectArgument = new Argument<string>(CommonLocalizableStrings.ProjectArgumentName)
        {
            Description = CommonLocalizableStrings.ProjectArgumentDescription
        }.DefaultToCurrentDirectory();

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("add", DocsLink, LocalizableStrings.NetAddCommand);

            command.AddArgument(ProjectArgument);
            command.AddCommand(AddPackageParser.GetCommand());
            command.AddCommand(AddProjectToProjectReferenceParser.GetCommand());

            command.SetHandler((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
