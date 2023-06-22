// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.Sln.Remove;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class SlnRemoveParser
    {
        public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new Argument<IEnumerable<string>>(LocalizableStrings.RemoveProjectPathArgumentName)
        {
            Description = LocalizableStrings.RemoveProjectPathArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("remove", LocalizableStrings.RemoveAppFullName);

            command.AddArgument(ProjectPathArgument);

            command.SetHandler((parseResult) => new RemoveProjectFromSolutionCommand(parseResult).Execute());

            return command;
        }
    }
}
