// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class NewCommandParser
    {
        public static readonly Argument Argument = new Argument<IEnumerable<string>>() { Arity = ArgumentArity.ZeroOrMore };

        public static readonly Option LanguageOption = new Option<string>(new string[] { "-lang", "--language" })
        {
            Argument = new Argument<string>()
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
