// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

using Microsoft.DotNet.Cli.Cleanup;
using LocalizableStrings = Microsoft.DotNet.Tools.Cleanup.LocalizableStrings;
using static Microsoft.DotNet.Cli.Cleanup.CleanupCommandCommon;

namespace Microsoft.DotNet.Cli
{
    internal static class CleanupStyleCommandParser
    {
        private static readonly CleanupStyleHandler s_styleHandler = new();

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

        class CleanupStyleHandler : ICommandHandler
        {
            public Task<int> InvokeAsync(InvocationContext context)
                => Task.FromResult(new CleanupStyleCommand().FromArgs(context.ParseResult).Execute());
        }
    }
}
