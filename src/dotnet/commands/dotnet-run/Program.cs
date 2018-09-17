// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public static RunCommand FromArgs(string[] args, string msbuildPath = null)
        {
            var result = Parser.Instance.ParseFrom("dotnet run", args);

            result.ShowHelpOrErrorIfAppropriate();

            var runCommand = result["dotnet"]["run"].Value<RunCommand>();
            return IncludingArgumentsAfterDoubleDash(runCommand, result.UnparsedTokens);
        }

        private static RunCommand IncludingArgumentsAfterDoubleDash(
            RunCommand runCommand,
            IEnumerable<string> unparsedTokens)
        {
            return runCommand.MakeNewWithReplaced(
                args: runCommand.Args
                    .Concat(unparsedTokens)
                    .ToList());
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            return FromArgs(args).Execute();
        }
    }
}
