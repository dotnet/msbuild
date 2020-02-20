// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolRunCommandParser
    {
        public static Command ToolRun()
        {
            return Create.Command(
                "run",
                LocalizableStrings.CommandDescription,
                Accept.ExactlyOneArgument(errorMessage: o => LocalizableStrings.SpecifyExactlyOneCommandName)
                    .With(name: LocalizableStrings.CommandNameArgumentName,
                          description: LocalizableStrings.CommandNameArgumentDescription),
                treatUnmatchedTokensAsErrors: false,
                options: CommonOptions.HelpOption());
        }
    }
}
