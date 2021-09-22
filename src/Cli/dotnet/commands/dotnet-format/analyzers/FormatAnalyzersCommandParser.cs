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
    internal static class FormatAnalyzersCommandParser
    {
        private static readonly FormatAnalyzersHandler s_analyzerHandler = new();

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("analyzers", LocalizableStrings.Run_3rd_party_analyzers__and_apply_fixes)
            {
                DiagnosticsOption,
                SeverityOption,
            };
            command.AddCommonOptions();
            command.Handler = s_analyzerHandler;
            return command;
        }

        class FormatAnalyzersHandler : ICommandHandler
        {
            public Task<int> InvokeAsync(InvocationContext context)
                => Task.FromResult(new FormatAnalyzersCommand().FromArgs(context.ParseResult).Execute());
        }
    }
}
