// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.New;

namespace Microsoft.DotNet.Cli
{
    internal static class NewCommandParser
    {
        public static readonly Argument Argument = new Argument() { Arity = ArgumentArity.ZeroOrMore };

        public static readonly Option LanguageOption = new Option(new string[] { "-lang", "--language" })
        {
            Argument = new Argument() { Arity = ArgumentArity.ExactlyOne }
        };

        public static Command GetCommand()
        {
            var command = new Command("new");

            command.AddOption(LanguageOption);
            command.AddArgument(Argument);

            return command;
        }
    }
}
