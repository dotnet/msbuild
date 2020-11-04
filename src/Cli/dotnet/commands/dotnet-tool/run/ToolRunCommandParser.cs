// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolRunCommandParser
    {
        public static readonly Argument CommandNameArgument = new Argument(LocalizableStrings.CommandNameArgumentName)
        {
            Description = LocalizableStrings.CommandNameArgumentDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static Command GetCommand()
        {
            var command = new Command("run", LocalizableStrings.CommandDescription);

            command.AddArgument(CommandNameArgument);

            return command;
        }
    }
}
