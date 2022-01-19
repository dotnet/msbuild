// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolRunCommandParser
    {
        public static readonly Argument<string> CommandNameArgument = new Argument<string>(LocalizableStrings.CommandNameArgumentName)
        {
            Description = LocalizableStrings.CommandNameArgumentDescription
        };

        public static Command GetCommand()
        {
            var command = new Command("run", LocalizableStrings.CommandDescription);

            command.AddArgument(CommandNameArgument);
            command.TreatUnmatchedTokensAsErrors = false;

            return command;
        }
    }
}
