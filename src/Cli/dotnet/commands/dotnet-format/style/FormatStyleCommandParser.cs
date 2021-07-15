// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;

using Microsoft.DotNet.Cli.Format;
using LocalizableStrings = Microsoft.DotNet.Tools.Format.LocalizableStrings;
using static Microsoft.DotNet.Cli.Format.FormatCommandCommon;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli
{
    internal static class FormatStyleCommandParser
    {
        private static readonly FormatStyleHandler s_styleHandler = new();

        public static Command GetCommand()
        {
            var command = new Command("style", LocalizableStrings.Run_code_style_analyzers_and_apply_fixes)
            {
                SeverityOption,
            };
            command.AddCommonOptions();
            command.Handler = s_styleHandler;
            return command;
        }

        class FormatStyleHandler : ICommandHandler
        {
            public Task<int> InvokeAsync(InvocationContext context)
                => Task.FromResult(new FormatStyleCommand().FromArgs(context.ParseResult).Execute());
        }
    }
}
