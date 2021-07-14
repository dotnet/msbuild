// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

using Microsoft.DotNet.Cli.Cleanup;
using LocalizableStrings = Microsoft.DotNet.Tools.Cleanup.LocalizableStrings;
using static Microsoft.DotNet.Cli.Cleanup.FormatCommandCommon;

namespace Microsoft.DotNet.Cli
{
    internal static partial class FormatCommandParser
    {
        private static readonly FormatCommandDefaultHandler s_cleanupCommandHandler = new();

        public static Command GetCommand()
        {
            var cleanupCommand = new Command("format", LocalizableStrings.Cleans_up_code_formatting_to_match_editorconfig_settings)
            {
                FormatWhitespaceCommandParser.GetCommand(),
                FormatStyleCommandParser.GetCommand(),
                FormatAnalyzersCommandParser.GetCommand(),
                DiagnosticsOption,
                SeverityOption,
            };
            cleanupCommand.AddCommonOptions();
            cleanupCommand.Handler = s_cleanupCommandHandler;
            return cleanupCommand;
        }

        class FormatCommandDefaultHandler : ICommandHandler
        {
            public Task<int> InvokeAsync(InvocationContext context)
                => Task.FromResult(new FormatCommand().FromArgs(context.ParseResult).Execute());
        }
    }
}
