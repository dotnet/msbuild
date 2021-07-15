// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Format;

using static Microsoft.DotNet.Cli.Format.FormatCommandCommon;

using LocalizableStrings = Microsoft.DotNet.Tools.Format.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class FormatWhitespaceCommandParser
    {
        private static readonly FormatWhitespaceHandler s_formattingHandler = new();
        public static Command GetCommand()
        {
            var command = new Command("whitespace", LocalizableStrings.Run_whitespace_formatting);
            command.AddCommonOptions();
            command.Handler = s_formattingHandler;
            return command;
        }

        class FormatWhitespaceHandler : ICommandHandler
        {
            public Task<int> InvokeAsync(InvocationContext context)
                => Task.FromResult(new FormatWhitespaceCommand().FromArgs(context.ParseResult).Execute());
        }
    }
}
