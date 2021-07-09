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
    internal static partial class CleanupCommandParser
    {
        private static readonly CleanupCommandDefaultHandler s_cleanupCommandHandler = new();

        public static Command GetCommand()
        {
            var cleanupCommand = new Command("cleanup", LocalizableStrings.Cleans_up_code_formatting_to_match_editorconfig_settings)
            {
                CleanupFormattingCommandParser.GetCommand(),
                CleanupStyleCommandParser.GetCommand(),
                CleanupAnalyzersCommandParser.GetCommand(),
                DiagnosticsOption,
                SeverityOption,
            };
            cleanupCommand.AddCommonOptions();
            cleanupCommand.Handler = s_cleanupCommandHandler;
            return cleanupCommand;
        }

        class CleanupCommandDefaultHandler : ICommandHandler
        {
            public Task<int> InvokeAsync(InvocationContext context)
                => Task.FromResult(new CleanupCommand().FromArgs(context.ParseResult).Execute());
        }
    }
}
