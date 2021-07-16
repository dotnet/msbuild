// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Help
{
    internal static class HelpCommandParser
    {
        public static readonly Argument<string> Argument = new Argument<string>(LocalizableStrings.CommandArgumentName)
        {
            Description = LocalizableStrings.CommandArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne
        };

        public static Command GetCommand()
        {
            var command = new Command("help", LocalizableStrings.AppFullName);

            command.AddArgument(Argument);

            return command;
        }
    }
}

