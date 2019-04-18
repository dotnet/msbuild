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
        public static RunCommand FromArgs(string[] args)
        {
            var result = Parser.Instance.ParseFrom("dotnet run", args);

            result.ShowHelpOrErrorIfAppropriate();

            var command = result["dotnet"]["run"].Value<RunCommand>();

            if (result.UnparsedTokens != null)
            {
                command.Args = command.Args.Concat(result.UnparsedTokens);
            }

            return command;
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            return FromArgs(args).Execute();
        }
    }
}
