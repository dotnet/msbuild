// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class SlnCommandParser
    {
        public static readonly Argument<string> SlnArgument = new Argument<string>(LocalizableStrings.SolutionArgumentName)
        {
            Description = LocalizableStrings.SolutionArgumentDescription,
            Arity = ArgumentArity.ExactlyOne
        }.DefaultToCurrentDirectory();

        public static Command GetCommand()
        {
            var command = new Command("sln", LocalizableStrings.AppFullName);

            command.AddArgument(SlnArgument);
            command.AddCommand(SlnAddParser.GetCommand());
            command.AddCommand(SlnListParser.GetCommand());
            command.AddCommand(SlnRemoveParser.GetCommand());

            command.Handler = CommandHandler.Create((Func<int>)(() => throw new Exception("TODO command not found")));

            return command;
        }
    }
}
