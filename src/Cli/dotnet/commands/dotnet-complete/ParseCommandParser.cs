// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class CompleteCommandParser
    {
        public static readonly Argument PathArgument = new Argument("path")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option PositionOption = new Option("--position")
        {
            Argument = new Argument("command")
            {
                Arity = ArgumentArity.ExactlyOne
            }
        };

        public static Command GetCommand()
        {
            var command = new Command("complete", string.Empty);

            command.AddArgument(PathArgument);
            command.AddOption(PositionOption);

            return command;
        }
    }
}
