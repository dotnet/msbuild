// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.List.ProjectToProjectReferences;
using LocalizableStrings = Microsoft.DotNet.Tools.List.ProjectToProjectReferences.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListProjectToProjectReferencesCommandParser
    {
        public static readonly Argument Argument = new Argument<string>("argument") { Arity = ArgumentArity.ZeroOrOne, IsHidden = true };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("reference", LocalizableStrings.AppFullName);

            command.AddArgument(Argument);

            command.SetHandler((parseResult) => new ListProjectToProjectReferencesCommand(parseResult).Execute());

            return command;
        }
    }
}
